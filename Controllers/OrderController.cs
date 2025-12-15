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
    public async Task<IActionResult> Checkout()
    {
        // Load danh mục để menu không bị lỗi
        ViewBag.Categories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();

        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account", new { returnUrl = "/Order/Checkout" });

        // Lấy giỏ hàng
        var cartItems = await _context.Carts.Include(c => c.Product).Where(c => c.UserId == userId).ToListAsync();
        if (!cartItems.Any()) return RedirectToAction("Index", "Cart");

        // Lấy info user để điền sẵn
        var user = await _context.Users.FindAsync(userId);

        ViewBag.CartItems = cartItems;
        ViewBag.TotalAmount = cartItems.Sum(c => (c.Quantity ?? 0) * (c.Product?.Price ?? 0));

        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> Checkout(string receiverName, string receiverPhone, string shippingAddress, string paymentMethod)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        var cartItems = await _context.Carts.Include(c => c.Product).Where(c => c.UserId == userId).ToListAsync();
        if (!cartItems.Any()) return RedirectToAction("Index", "Cart");

        // --- XỬ LÝ KHÔNG SỬA MODEL ---
        // Gộp Tên + SĐT + Địa chỉ vào chung 1 cột ShippingAddress
        string fullAddress = $"{receiverName} ({receiverPhone}) - {shippingAddress}";

        // 1. Tạo đơn hàng
        var order = new Order
        {
            UserId = userId,
            OrderDate = DateTime.Now,
            Status = "Pending", // Mới đặt thì là Pending
            ShippingAddress = fullAddress, // Lưu chuỗi đã gộp
            PaymentMethod = paymentMethod,
            PaymentStatus = (paymentMethod == "Online") ? "Paid" : "Unpaid",
            TotalAmount = cartItems.Sum(c => (c.Quantity ?? 0) * (c.Product?.Price ?? 0))
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // 2. Lưu chi tiết đơn (OrderDetail)
        foreach (var item in cartItems)
        {
            var orderDetail = new OrderDetail
            {
                OrderId = order.OrderId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.Product?.Price // Giá lúc mua
            };
            _context.OrderDetails.Add(orderDetail);

            // Trừ kho (nếu muốn)
            if (item.Product != null) item.Product.Quantity -= item.Quantity;
        }

        // 3. Xóa giỏ hàng
        _context.Carts.RemoveRange(cartItems);
        await _context.SaveChangesAsync();

        return RedirectToAction("OrderSuccess", new { id = order.OrderId });
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
}