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
    public async Task<IActionResult> Checkout(List<int> selectedIds)
    {
        ViewBag.Categories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();

        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account", new { returnUrl = "/Order/Checkout" });

        // 1. Lấy sản phẩm được chọn
        var cartItems = await _context.Carts.Include(c => c.Product)
            .Where(c => c.UserId == userId && selectedIds.Contains(c.CartId)).ToListAsync();
        
        if (!cartItems.Any()) return RedirectToAction("Index", "Cart");

        // 2. Tính tổng tiền
        decimal totalAmount = cartItems.Sum(c => (c.Quantity ?? 0) * (c.Product?.Price ?? 0));


        // 3. Lấy User
        var user = await _context.Users.FindAsync(userId);

        // 4. Lấy Coupon (DÙNG MODEL CHUẨN)
        // Lưu ý: Nếu báo lỗi ở dòng này nghĩa là bạn chưa làm bước 2 (thêm DbSet vào Context)
        var userCoupons = await _context.UserCoupons
            .Where(u => u.UserId == userId && u.IsActive)
            .ToListAsync();
        
        ViewBag.UserCoupons = userCoupons;
        ViewBag.CartItems = cartItems;
        ViewBag.TotalAmount = totalAmount;

        return View(user);
    }

    // --- 2. XỬ LÝ THANH TOÁN (POST) ---
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(List<int> selectedIds, Order orderInfo, string PaymentMethod, int? selectedCouponId)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        var cartItems = await _context.Carts.Include(c => c.Product)
            .Where(c => c.UserId == userId && selectedIds.Contains(c.CartId)).ToListAsync();
        
        if (!cartItems.Any()) return RedirectToAction("Index", "Cart");

        // 1. Xử lý Coupon & Tìm Coupon Gốc (Master)
        UserCoupon userCoupon = null;
        Coupon masterCoupon = null;
        decimal globalDiscount = 0;
        
        if (selectedCouponId.HasValue)
        {
            userCoupon = await _context.UserCoupons
                .FirstOrDefaultAsync(u => u.UserCouponId == selectedCouponId.Value && u.UserId == userId);
            
            if (userCoupon != null)
            {
                globalDiscount = userCoupon.DiscountAmount;
                // [QUAN TRỌNG] Tìm Coupon gốc để lấy ID lưu vào lịch sử
                masterCoupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == userCoupon.Code);
            }
        }

        // Tính tổng tiền toàn bộ giỏ (để chia tỷ lệ giảm giá)
        decimal totalCartValue = cartItems.Sum(x => (x.Product?.Price ?? 0) * (x.Quantity ?? 0));

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Tạo Transaction Group (Tổng giao dịch)
            decimal transactionTotal = totalCartValue - globalDiscount;
            if (transactionTotal < 0) transactionTotal = 0;

            var transactionGroup = new TransactionGroup
            {
                UserId = userId,
                CreatedAt = DateTime.Now,
                TotalAmount = transactionTotal,
                PaymentMethod = PaymentMethod,
                PaymentStatus = "Unpaid"
            };
            _context.TransactionGroups.Add(transactionGroup);
            await _context.SaveChangesAsync();

            // Nhóm theo Seller để tạo các đơn hàng con
            var groupBySeller = cartItems.GroupBy(x => x.Product?.SellerId ?? 0);
            
            foreach (var sellerGroup in groupBySeller)
            {
                if (sellerGroup.Key == 0) continue;

                decimal groupSubtotal = sellerGroup.Sum(x => (x.Product?.Price ?? 0) * (x.Quantity ?? 0));
                decimal shippingFee = 30000;

                // [LOGIC MỚI] CHIA TỶ LỆ TIỀN GIẢM GIÁ CHO ĐƠN HÀNG NÀY
                decimal groupDiscount = 0;
                if (totalCartValue > 0 && globalDiscount > 0)
                {
                    // Công thức: Tiền giảm = Tổng giảm * (Tiền hàng shop này / Tổng tiền giỏ)
                    groupDiscount = globalDiscount * (groupSubtotal / totalCartValue);
                }

                var order = new Order
                {
                    TransactionGroupId = transactionGroup.TransactionGroupId,
                    SellerId = sellerGroup.Key,
                    UserId = userId,
                    Subtotal = groupSubtotal,
                    ShippingFee = shippingFee,
                    DiscountAmount = groupDiscount, // Lưu số tiền được giảm
                    
                    // [QUAN TRỌNG] Tổng tiền phải trả = (Tiền hàng + Ship) - Tiền giảm
                    TotalAmount = (groupSubtotal + shippingFee) - groupDiscount, 
                    
                    Status = "Pending",
                    PaymentMethod = PaymentMethod,
                    PaymentStatus = "Unpaid",
                    ShippingAddress = orderInfo.ShippingAddress,
                    ReceiverName = orderInfo.ReceiverName,
                    ReceiverPhone = orderInfo.ReceiverPhone,
                    OrderDate = DateTime.Now
                };
                
                // Đảm bảo không âm tiền
                if (order.TotalAmount < 0) order.TotalAmount = 0;

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // Lưu để lấy OrderId

                // Lưu chi tiết sản phẩm
                foreach (var item in sellerGroup)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.OrderId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity ?? 0,
                        UnitPrice = item.Product?.Price ?? 0
                    };
                    _context.OrderDetails.Add(orderDetail);
                    
                    if (item.Product != null)
                    {
                        item.Product.Quantity -= (item.Quantity ?? 0);
                    }
                }

                // [MỚI] LƯU LỊCH SỬ DÙNG COUPON (VÀO DATABASE)
                if (groupDiscount > 0 && masterCoupon != null)
                {
                    var usage = new CouponUsage
                    {
                        CouponId = masterCoupon.CouponId, // ID của Coupon gốc
                        UserId = userId,
                        OrderId = order.OrderId,          // Gắn vào đơn hàng này
                        AppliedAmount = groupDiscount,    // Số tiền giảm thực tế cho đơn này
                        UsedAt = DateTime.Now,
                        Note = "Khách dùng mã từ ví UserCoupon"
                    };
                    _context.CouponUsages.Add(usage);
                }
            }

            // Dọn dẹp
            _context.Carts.RemoveRange(cartItems);
            if (userCoupon != null) _context.UserCoupons.Remove(userCoupon); // Xóa mã khỏi ví khách

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = "Đặt hàng thành công!";
            return RedirectToAction("OrderSuccess", new { id = transactionGroup.TransactionGroupId });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "Lỗi: " + ex.Message;
            return RedirectToAction("Checkout", new { selectedIds });
        }
    }
    // --- 2. THÔNG BÁO & LỊCH SỬ ---
    public IActionResult OrderSuccess(Guid id)
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
            .Include(o => o.CouponUsages).ThenInclude(cu => cu.Coupon) 
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