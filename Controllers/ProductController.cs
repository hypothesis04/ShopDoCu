
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShopDoCu.Models;
using ShopDoCu.Views.Product;

namespace ShopDoCu.Controllers;

// Controller quản lý sản phẩm - đăng bán, xem chi tiết, tìm kiếm
public class ProductController : Controller
{
    private readonly ChoBanDoCuContext _context;
    private readonly IWebHostEnvironment _environment;

    public ProductController(ChoBanDoCuContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }
    [HttpGet]
    public async Task<IActionResult> Index(string q, int? categoryId, decimal? minPrice, decimal? maxPrice)
    {
        // 1. Khởi tạo query
        var productsQuery = _context.Products
            .Include(p => p.ProductImages)
            .Include(p => p.Category)
            .Where(p => p.Status == "Active");

       
        // 2. TÌM KIẾM
        if (!string.IsNullOrWhiteSpace(q))
        {
            // Nếu từ khóa ngắn (<= 3 ký tự) -> Chỉ tìm tên (tránh ra IPS, Chip...)
            productsQuery = productsQuery.Where(p => (p.ProductName ?? "").Contains(q));
       
        }

        // 3. DANH MỤC
        if (categoryId.HasValue)
        {
            var childCategoryIds = await _context.Categories
                .Where(c => c.ParentId == categoryId)
                .Select(c => c.CategoryId)
                .ToListAsync();
            childCategoryIds.Add(categoryId.Value);

            productsQuery = productsQuery.Where(p => p.CategoryId.HasValue && childCategoryIds.Contains(p.CategoryId.Value));
        }

        // 4. LỌC GIÁ
        if (minPrice.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.Price >= minPrice.Value);
        }
        if (maxPrice.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.Price <= maxPrice.Value);
        }

        // 5. Menu danh mục bên trái
        var categories = await _context.Categories
            .Include(c => c.InverseParent)
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();

        // 6. ViewBag
        ViewBag.Categories = categories;
        ViewBag.SelectedCategoryId = categoryId;
        ViewBag.SearchQuery = q;
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;

        // 7. Thực thi
        var result = await productsQuery
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View("Index", result);
    }
    // API endpoint để lấy danh mục con theo danh mục cha (dùng cho AJAX)
    [HttpGet]
    public async Task<IActionResult> GetChildCategories(int parentId)
    {
        var childCategories = await _context.Categories
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.CategoryName)
            .Select(c => new { c.CategoryId, c.CategoryName })
            .ToListAsync();

        return Json(childCategories);
    }
    // 1. Action Xem chi tiết sản phẩm (Dành cho khách hàng)
    public async Task<IActionResult> Details(int id)
    {
        var product = await _context.Products
            .Include(p => p.Seller)
            .Include(p => p.Category)
            .Include(p => p.ProductImages) 
            .FirstOrDefaultAsync(m => m.ProductId == id);

        if (product == null) return NotFound();

        // Logic tăng lượt xem (Views) mỗi khi có người bấm vào
        product.Views = (product.Views ?? 0) + 1;
        await _context.SaveChangesAsync();

        var relatedProducts = await _context.Products
        .Include(p => p.ProductImages)
        .Where(p => p.CategoryId == product.CategoryId && p.ProductId != id && p.Quantity > 0)
        .OrderByDescending(p => p.CreatedAt) // Lấy mới nhất
        .Take(4)
        .ToListAsync();

        // Truyền sang View
        ViewBag.RelatedProducts = relatedProducts;
        // --- 4. LOGIC MỚI: KIỂM TRA QUYỀN ĐÁNH GIÁ ---
        bool canReview = false;
        var userId = HttpContext.Session.GetInt32("UserId");
        
        if (userId != null)
        {
            // Kiểm tra xem User này có đơn hàng nào chứa sản phẩm này VÀ đã Hoàn thành không
            canReview = await _context.OrderDetails
                .Include(od => od.Order)
                .AnyAsync(od => od.ProductId == id 
                                && od.Order.UserId == userId 
                                && od.Order.Status == "Completed");
        }
        // Điều kiện: Của đúng shop này + Đang kích hoạt + Còn số lượng + Chưa hết hạn
        var shopCoupons = await _context.Coupons
            .Where(c => c.SellerId == product.SellerId 
                        && c.IsActive == true 
                        && c.Quantity > 0 
                        && c.EndDate >= DateTime.Now)
            .ToListAsync();
        
        ViewBag.ShopCoupons = shopCoupons; // Truyền sang View
    
   
        
        // Truyền biến này sang View để ẩn/hiện form
        ViewBag.CanReview = canReview;
        return View(product);
    }
}
