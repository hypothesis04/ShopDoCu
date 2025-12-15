
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
    public async Task<IActionResult> Create(bool success = false)
    {
        // 1. Kiểm tra đăng nhập & Quyền Seller
        var userId = HttpContext.Session.GetInt32("UserId");
        var role = HttpContext.Session.GetString("Role");

        if (userId == null) return RedirectToAction("Login", "Account", new { returnUrl = "/Product/Create" });
        if (role != "Seller") return RedirectToAction("SellerRegistration", "Account");

        // 2. Chuẩn bị dữ liệu danh mục cha
        var parentCategories = await _context.Categories
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();

        ViewBag.ParentCategories = new SelectList(parentCategories, "CategoryId", "CategoryName");
        
        // 3. Truyền cờ thành công sang View
        ViewBag.IsSuccess = success; 

        return View(new ProductCreateViewModel());
    }

    // ==========================================
    // 2. HÀM XỬ LÝ LƯU (POST: /Product/Create)
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCreateViewModel model)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        var role = HttpContext.Session.GetString("Role");

        if (userId == null || role != "Seller") return RedirectToAction("Login", "Account");

        // Validate ảnh thủ công
        var imageFiles = Request.Form.Files.Where(f => f.Name == "ProductImages").ToList();
        if (imageFiles == null || imageFiles.Count < 3)
        {
            ModelState.AddModelError("ProductImages", "Vui lòng chọn ít nhất 3 ảnh sản phẩm");
        }

        if (imageFiles != null && (model.MainImageIndex < 0 || model.MainImageIndex >= imageFiles.Count))
        {
            ModelState.AddModelError("MainImageIndex", "Vui lòng chọn ảnh chính hợp lệ");
        }

        // Nếu dữ liệu sai -> Trả về View để sửa
        if (!ModelState.IsValid)
        {
            var parentCategories = await _context.Categories.Where(c => c.ParentId == null).ToListAsync();
            ViewBag.ParentCategories = new SelectList(parentCategories, "CategoryId", "CategoryName");

            if (model.ParentCategoryId.HasValue)
            {
                var childCategories = await _context.Categories.Where(c => c.ParentId == model.ParentCategoryId).ToListAsync();
                ViewBag.ChildCategories = new SelectList(childCategories, "CategoryId", "CategoryName");
            }
            return View(model);
        }

        // BẮT ĐẦU TRANSACTION
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            var savedFilePaths = new List<string>();
            try
            {
                // 1. Lưu Product
                var product = new Product
                {
                    ProductName = model.ProductName,
                    Description = model.Description,
                    Price = model.Price,
                    Quantity = model.Quantity,
                    IsNew = model.IsNew,
                    CategoryId = model.CategoryId,
                    SellerId = userId,
                    Location = model.Location,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    Views = 0
                };
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // 2. Lưu Ảnh
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "products");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                for (int i = 0; i < imageFiles.Count; i++)
                {
                    var file = imageFiles[i];
                    if (file.Length > 0)
                    {
                        var fileName = $"{product.ProductId}_{i}_{DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}";
                        var filePath = Path.Combine(uploadsFolder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                        savedFilePaths.Add(filePath); // Lưu đường dẫn để rollback nếu lỗi

                        var pImage = new ProductImage
                        {
                            ProductId = product.ProductId,
                            ImageUrl = $"/images/products/{fileName}",
                            IsMain = (i == model.MainImageIndex)
                        };
                        _context.ProductImages.Add(pImage);
                    }
                }
                await _context.SaveChangesAsync();

                // 3. Commit
                await transaction.CommitAsync();

                // --- QUAN TRỌNG: Redirect về chính nó kèm success=true ---
                return RedirectToAction("Create", new { success = true });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                foreach (var path in savedFilePaths)
                {
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
                throw;
            }
        }
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
            .Include(p => p.ProductImages) // Nhớ include ảnh để hiện slider
            .FirstOrDefaultAsync(m => m.ProductId == id);

        if (product == null) return NotFound();

        // Logic tăng lượt xem (Views) mỗi khi có người bấm vào
        product.Views = (product.Views ?? 0) + 1;
        await _context.SaveChangesAsync();

        return View(product);
    }

    // 2. Action Thêm vào giỏ hàng (Xử lý khi bấm nút Mua)
    [HttpPost]
    [HttpPost]
    public async Task<IActionResult> AddToCart(int productId, int quantity)
    {
        // 1. Kiểm tra đăng nhập
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            // Lưu URL hiện tại để login xong quay lại đúng trang chi tiết sản phẩm
            return RedirectToAction("Login", "Account", new { returnUrl = $"/Product/Details/{productId}" });
        }

        // 2. Kiểm tra xem sản phẩm đã có trong giỏ của user này chưa
        var cartItem = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

        if (cartItem != null)
        {
            // Nếu có rồi -> Cộng dồn số lượng
            // (Lưu ý: Quantity là int? nên cần check null)
            cartItem.Quantity = (cartItem.Quantity ?? 0) + quantity;
        }
        else
        {
            // Nếu chưa có -> Tạo mới
            cartItem = new Cart
            {
                UserId = userId,
                ProductId = productId,
                Quantity = quantity,
                AddedAt = DateTime.Now 
            };
            _context.Carts.Add(cartItem);
        }

        await _context.SaveChangesAsync();

        // Chuyển hướng đến trang Giỏ hàng để người dùng xem luôn
        return RedirectToAction("Index", "Cart");
    }
     // Trang danh sách sản phẩm, lọc theo danh mục nếu có
  [HttpGet]
    public async Task<IActionResult> Index(int? categoryId)
    {
        // 1. Khởi tạo query cơ bản
        var productsQuery = _context.Products
            .Include(p => p.ProductImages)
            .Include(p => p.Category)
            .Where(p => p.Status == "Active");

        // 2. Xử lý lọc theo danh mục
        if (categoryId.HasValue)
        {
            // Tìm tất cả các danh mục con của danh mục đang chọn
            // Ví dụ: Chọn "Điện thoại" (ID 1) thì sẽ tìm ra [20, 21, 22, 23...] (iPhone, Samsung...)
            var childCategoryIds = await _context.Categories
                .Where(c => c.ParentId == categoryId)
                .Select(c => c.CategoryId)
                .ToListAsync();

            // Thêm chính ID đang chọn vào danh sách (để lỡ sản phẩm gán trực tiếp vào cha vẫn hiện)
            childCategoryIds.Add(categoryId.Value);

            // Lọc sản phẩm có CategoryId nằm trong danh sách này
            productsQuery = productsQuery.Where(p => p.CategoryId.HasValue && childCategoryIds.Contains(p.CategoryId.Value));
        }

        // 3. Lấy danh sách danh mục cha để hiển thị lên Menu
        var categories = await _context.Categories
            .Include(c => c.InverseParent) // <--- QUAN TRỌNG: Lấy kèm con để hiển thị
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();

        ViewBag.Categories = categories;
        ViewBag.SelectedCategoryId = categoryId;

        // 4. Thực thi query và trả về View
        var result = await productsQuery
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(result);
    }
    // Tìm kiếm sản phẩm
[HttpGet]
public async Task<IActionResult> TimKiem(string q)
{
    // 1. Tìm kiếm sản phẩm
    var products = _context.Products
        .Include(p => p.ProductImages)
        .Include(p => p.Category)
        .Where(p => p.Status == "Active");

    if (!string.IsNullOrWhiteSpace(q))
    {
        products = products.Where(p => p.ProductName.Contains(q) || p.Description.Contains(q));
    }

    // 2. Lấy danh mục cho Menu (SỬA ĐOẠN NÀY)
    // Phải Include 'InverseParent' để lấy được danh mục con
    var categories = await _context.Categories
        .Include(c => c.InverseParent) // <--- QUAN TRỌNG: Để hiển thị menu đa cấp
        .Where(c => c.ParentId == null)
        .OrderBy(c => c.CategoryName)
        .ToListAsync();

    ViewBag.Categories = categories;
    ViewBag.SearchQuery = q;

    // Trả về View Index để tái sử dụng giao diện
    return View("Index", await products.OrderByDescending(p => p.CreatedAt).ToListAsync());
}
}
