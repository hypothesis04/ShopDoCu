using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopDoCu.Models;

namespace ShopDoCu.Controllers;

public class CartController : Controller
{
    private readonly ChoBanDoCuContext _context;

    public CartController(ChoBanDoCuContext context)
    {
        _context = context;
    }
    public async Task<IActionResult> Index()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        var cartItems = await _context.Carts
            .Include(c => c.Product)
            .ThenInclude(p => p.ProductImages)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.AddedAt)
            .ToListAsync();

        return View(cartItems);
    }

    // 2. CẬP NHẬT SỐ LƯỢNG (ĐÃ SỬA ĐỔI)
    // Đổi tên thành UpdateCart để khớp với AJAX bên View
    // Dùng productId thay vì cartId để dễ truy vấn tồn kho
    [HttpPost]
    public async Task<IActionResult> UpdateCart(int productId, int quantity)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Json(new { success = false, message = "Vui lòng đăng nhập!" });

        var product = await _context.Products.FindAsync(productId);
        if (product == null) return Json(new { success = false, message = "Sản phẩm không tồn tại!" });

        // --- RÀNG BUỘC: NGƯỜI BÁN KHÔNG ĐƯỢC MUA HÀNG CỦA MÌNH ---
        if (product.SellerId == userId)
            {
                return Json(new { success = false, message = "Bạn không thể tự mua sản phẩm do chính mình đăng bán!" });
            }
        // Tìm sản phẩm trong giỏ của user này
        // Phải Include Product để lấy số lượng tồn kho (Quantity)
        var cartItem = await _context.Carts
            .Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

        if (cartItem == null) 
        {
            return Json(new { success = false, message = "Sản phẩm không có trong giỏ hàng!" });
        }

        // --- LOGIC KIỂM TRA TỒN KHO ---
        // Nếu khách muốn mua nhiều hơn số hàng trong kho
        if (quantity > cartItem.Product.Quantity)
        {
            return Json(new { 
                success = false, 
                message = $"Kho chỉ còn {cartItem.Product.Quantity} sản phẩm!", 
                maxQuantity = cartItem.Product.Quantity // Trả về số max để View tự điền
            });
        }

        // Nếu hợp lệ thì cập nhật
        if (quantity > 0)
        {
            cartItem.Quantity = quantity;
            await _context.SaveChangesAsync();
        }
        else 
        {
            // Nếu gửi số 0 hoặc âm thì reset về 1 (không xóa, để nút xóa lo việc xóa)
             cartItem.Quantity = 1;
             await _context.SaveChangesAsync();
        }

        // --- TÍNH TOÁN LẠI TIỀN ĐỂ CẬP NHẬT GIAO DIỆN ---
        // Lấy lại toàn bộ giỏ để tính tổng tiền (Grand Total)
        var cartItems = await _context.Carts
            .Include(c => c.Product)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        var itemTotal = (cartItem.Quantity * cartItem.Product.Price); // Thành tiền món này
        var cartTotal = cartItems.Sum(c => c.Quantity * c.Product.Price); // Tổng tiền cả giỏ

        // Trả về JSON cho AJAX
        return Json(new { 
            success = true, 
            itemTotal = itemTotal?.ToString("N0") + " đ", 
            cartTotal = cartTotal?.ToString("N0") + " đ" 
        });
    }

    // 3. Xóa sản phẩm khỏi giỏ (Sửa lại chút để nhận productId cho tiện)
    public async Task<IActionResult> Remove(int productId)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        var cartItem = await _context.Carts
            .FirstOrDefaultAsync(c => c.ProductId == productId && c.UserId == userId);
            
        if (cartItem != null)
        {
            _context.Carts.Remove(cartItem);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Index");
    }
}