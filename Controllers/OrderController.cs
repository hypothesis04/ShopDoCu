using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopDoCu.Models;

namespace ShopDoCu.Controllers;

public class OrderController : Controller
{
    private readonly ChoBanDoCuContext _context;

    public OrderController(ChoBanDoCuContext context)
    {
        _context = context;
    }

    // --- 1. TRANG THANH TOÁN ---
    [HttpGet]
    // GET: /Order/Checkout
    public async Task<IActionResult> Checkout()
    {
        // 1. Load danh mục (để menu không lỗi)
        ViewBag.Categories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();

        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account", new { returnUrl = "/Order/Checkout" });

        // 2. Lấy giỏ hàng
        var cartItems = await _context.Carts.Include(c => c.Product).Where(c => c.UserId == userId).ToListAsync();
        if (!cartItems.Any()) return RedirectToAction("Index", "Cart");
        
        // 3. Tính tổng tiền
        // Lưu ý: Dùng (c.Quantity ?? 0) để tránh lỗi nếu null
        decimal totalAmount = cartItems.Sum(c => (c.Quantity ?? 0) * c.Product.Price);
        // 4. Lấy thông tin User để điền sẵn vào form
        var user = await _context.Users.FindAsync(userId);
        
        ViewBag.CartItems = cartItems;
        ViewBag.TotalAmount = totalAmount; // Gửi tổng tiền sang View (để hiển thị)
        ViewBag.User = user; // Gửi user sang View (để điền form)

        // Gửi tổng tiền để check > 20tr
        return View(user);
    }
    // POST: /Order/Checkout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(Order orderInfo, string PaymentMethod, string CouponCode) // Thêm tham số CouponCode
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        // 1. Lấy giỏ hàng
        var cartItems = await _context.Carts.Include(c => c.Product).Where(c => c.UserId == userId).ToListAsync();
        if (!cartItems.Any()) return RedirectToAction("Index", "Cart");

        decimal totalAmount = cartItems.Sum(c => (c.Quantity ?? 0) * c.Product.Price);
        decimal discountAmount = 0; // Mặc định giảm 0đ

        // --- LOGIC XỬ LÝ MÃ GIẢM GIÁ (SERVER SIDE) ---
        if (!string.IsNullOrEmpty(CouponCode))
        {
            var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == CouponCode && c.IsActive);
            
            // Kiểm tra lại các điều kiện cho chắc chắn
            if (coupon != null && coupon.Quantity > 0 && 
                DateTime.Now >= coupon.StartDate && DateTime.Now <= coupon.EndDate &&
                totalAmount >= coupon.MinOrderAmount)
            {
                // Tính toán giảm giá
                if (coupon.DiscountType == "Percent")
                    discountAmount = totalAmount * (coupon.DiscountValue / 100);
                else
                    discountAmount = coupon.DiscountValue;

                if (discountAmount > totalAmount) discountAmount = totalAmount;

                // Trừ số lượng mã đi 1
                coupon.Quantity -= 1;
            }
        }
        // ---------------------------------------------

        // 2. Tạo đơn hàng
        var order = new Order
        {
            UserId = userId,
            OrderDate = DateTime.Now,
            TotalAmount = totalAmount - discountAmount, // Lưu số tiền ĐÃ GIẢM
            DiscountAmount = discountAmount,            // Lưu số tiền được giảm
            CouponCode = discountAmount > 0 ? CouponCode : null, // Lưu mã (nếu áp dụng thành công)
            
            Status = "Pending",
            PaymentMethod = PaymentMethod,
            PaymentStatus = (PaymentMethod == "Online") ? "Unpaid" : "Unpaid",
            ShippingAddress = orderInfo.ShippingAddress,
            ReceiverName = orderInfo.ReceiverName,
            ReceiverPhone = orderInfo.ReceiverPhone
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // 3. Lưu chi tiết đơn (Giữ nguyên code cũ) ...
        foreach (var item in cartItems)
        {
            var orderDetail = new OrderDetail
            {
                OrderId = order.OrderId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.Product.Price
            };
            _context.OrderDetails.Add(orderDetail);
            
            // Trừ kho sản phẩm
            item.Product.Quantity -= item.Quantity;
        }

        _context.Carts.RemoveRange(cartItems);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đặt hàng thành công! Mã đơn: #" + order.OrderId;
        return RedirectToAction("Index", "Home");
    }
    // --- 2. THÔNG BÁO & LỊCH SỬ ---
    public IActionResult OrderSuccess(int id)
    {
        ViewBag.Categories = _context.Categories.Where(c => c.ParentId == null).ToList();
        return View(id);
    }

    public async Task<IActionResult> MyOrders()
    {
        ViewBag.Categories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        var orders = await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewBag.Categories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();
        var userId = HttpContext.Session.GetInt32("UserId");

        var order = await _context.Orders
            .Include(o => o.OrderDetails)
            .ThenInclude(od => od.Product)
            .ThenInclude(p => p.ProductImages)
            .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null) return NotFound();

        return View(order);
    }
    // HÀM MỚI: Khách xác nhận đã nhận được hàng
    [HttpPost]
    public async Task<IActionResult> ConfirmReceipt(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        // Chỉ xác nhận được khi đơn đang ở trạng thái Shipping
        if (order != null && order.Status == "Shipping")
        {
            order.Status = "Completed"; // Chuyển sang hoàn thành
            
            // Nếu là COD thì mặc định nhận hàng xong là đã trả tiền
            if (order.PaymentMethod == "COD")
            {
                order.PaymentStatus = "Paid";
                order.PaymentDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cảm ơn bạn! Đơn hàng đã hoàn tất.";
        }

        return RedirectToAction("Details", new { id = id });
    }
    // API: Kiểm tra mã giảm giá (Dùng cho AJAX)
    [HttpGet]
    public async Task<IActionResult> CheckCoupon(string code, decimal totalAmount)
    {
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Code == code && c.IsActive);

        if (coupon == null)
        {
            return Json(new { success = false, message = "Mã giảm giá không tồn tại hoặc đã bị khóa!" });
        }

        if (DateTime.Now < coupon.StartDate || DateTime.Now > coupon.EndDate)
        {
            return Json(new { success = false, message = "Mã giảm giá chưa bắt đầu hoặc đã hết hạn!" });
        }

        if (coupon.Quantity <= 0)
        {
            return Json(new { success = false, message = "Mã giảm giá đã hết lượt sử dụng!" });
        }

        if (totalAmount < coupon.MinOrderAmount)
        {
            return Json(new { success = false, message = $"Đơn hàng phải từ {coupon.MinOrderAmount:N0}₫ mới được dùng mã này!" });
        }

        // Tính toán số tiền giảm
        decimal discount = 0;
        if (coupon.DiscountType == "Percent")
        {
            discount = totalAmount * (coupon.DiscountValue / 100);
        }
        else // Fixed
        {
            discount = coupon.DiscountValue;
        }

        // Không cho giảm quá số tiền đơn hàng (tránh âm tiền)
        if (discount > totalAmount) discount = totalAmount;

        var newTotal = totalAmount - discount;

        return Json(new { 
            success = true, 
            message = "Áp dụng mã thành công!", 
            discountAmount = discount, 
            discountText = discount.ToString("N0") + "₫",
            newTotal = newTotal.ToString("N0") + "₫"
        });
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        var order = await _context.Orders
            .Include(o => o.OrderDetails) // Quan trọng: lấy chi tiết để hoàn kho
            .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null) return NotFound();

        // Chỉ cho phép hủy khi đang Chờ xác nhận
        if (order.Status == "Pending")
        {
            order.Status = "Cancelled";
            
            // --- LOGIC HOÀN KHO (QUAN TRỌNG) ---
            // Khi hủy đơn, phải cộng lại số lượng sản phẩm vào kho
            if (order.OrderDetails != null)
            {
                foreach(var item in order.OrderDetails)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if(product != null)
                    {
                        product.Quantity += item.Quantity; // Cộng lại kho
                    }
                }
            }
            // -----------------------------------

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã hủy đơn hàng thành công và hoàn lại kho.";
        }
        else 
        {
            TempData["ErrorMessage"] = "Không thể hủy đơn hàng này (Đã giao hoặc đang vận chuyển).";
        }

        return RedirectToAction("Details", new { id = id });
    }

    // 2. Xử lý Yêu cầu Trả hàng (Khi đã nhận hàng)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestReturn(int id, string reason)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null) return NotFound();

        // Chỉ cho trả hàng khi trạng thái là Completed (Đã nhận hàng)
        if (order.Status == "Completed")
        {
            order.Status = "ReturnRequested"; // Đổi trạng thái
            // Lưu ý: Nếu bạn chưa có cột "Note" trong bảng Order thì bỏ dòng dưới, 
            // hoặc chạy migration thêm cột Note vào DB để lưu lý do trả hàng.
            // order.Note = reason; 
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã gửi yêu cầu trả hàng. Shop sẽ liên hệ với bạn.";
        }
        else
        {
            TempData["ErrorMessage"] = "Đơn hàng chưa đủ điều kiện trả hàng.";
        }

        return RedirectToAction("Details", new { id = id });
    }
}