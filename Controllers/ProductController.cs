
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
            if (q.Length <= 3) 
            {
                productsQuery = productsQuery.Where(p => (p.ProductName ?? "").Contains(q));
            }
            // Nếu từ khóa dài -> Tìm cả tên và mô tả
            else 
            {
                productsQuery = productsQuery.Where(p => (p.ProductName ?? "").Contains(q) || (p.Description ?? "").Contains(q));
            }
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

   [HttpGet]
    public async Task<IActionResult> Create(bool success = false)
    {
        // 1. Kiểm tra đăng nhập
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account", new { returnUrl = "/Product/Create" });

        // 2. Lấy Role từ Session
        var role = HttpContext.Session.GetString("Role");

        // --- ĐOẠN CODE FIX LỖI LOOP (VÒNG LẶP) ---
        // Nếu Session nói "không phải Seller", khoan hãy đuổi đi.
        // Hãy kiểm tra Database xem Admin đã duyệt chưa mà Session chưa kịp cập nhật.
        if (role != "Seller")
        {
            var userInDb = await _context.Users.FindAsync(userId);
            
            // Nếu trong DB đã là Seller rồi -> Cập nhật lại Session ngay
            if (userInDb != null && userInDb.Role == "Seller")
            {
                HttpContext.Session.SetString("Role", "Seller");
                role = "Seller"; // Gán lại biến local để chạy tiếp code bên dưới
            }
            else
            {
                // Nếu DB vẫn chưa duyệt thật -> Lúc này mới đuổi về trang đăng ký
                return RedirectToAction("SellerRegistration", "Account");
            }
        }
        // ------------------------------------------

        // 3. Chuẩn bị dữ liệu danh mục cha
        var parentCategories = await _context.Categories
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();

        ViewBag.ParentCategories = new SelectList(parentCategories, "CategoryId", "CategoryName");
        
        // 4. Truyền cờ thành công sang View
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

        // Lấy file ảnh
        var imageFiles = Request.Form.Files.Where(f => f.Name == "ProductImages").ToList();

        // Validate ảnh thủ công
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
                await _context.SaveChangesAsync(); // Lưu để lấy ProductId

                // 2. Lưu Ảnh
                // Lưu ý: Cần inject IWebHostEnvironment _environment vào Constructor của Controller
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "products");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                if (imageFiles != null)
                {
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
                    } // Đóng vòng For
                } // Đóng IF imageFiles != null 

                await _context.SaveChangesAsync(); // Lưu ProductImages vào DB

                // 3. Commit
                await transaction.CommitAsync();

                return RedirectToAction("Create", new { success = true });
            }
            catch (Exception)
            {
                // Rollback transaction (tự động khi dispose, nhưng gọi explicit cũng được)
                await transaction.RollbackAsync();

                // Xóa ảnh đã lỡ upload nếu lỗi DB
                foreach (var path in savedFilePaths)
                {
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
                throw; // Ném lỗi ra để hiển thị trang lỗi hoặc log
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
    public async Task<IActionResult> AddToCart(int productId, int quantity)
    {
        // 1. Kiểm tra đăng nhập
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            // Lưu URL hiện tại để login xong quay lại đúng trang chi tiết sản phẩm
            return RedirectToAction("Login", "Account", new { returnUrl = $"/Product/Details/{productId}" });
        }
        var product = await _context.Products.FindAsync(productId);
    
        if (product != null && product.SellerId == userId)
        {
            // Nếu người mua trùng với người bán -> Báo lỗi và đuổi về
            TempData["ErrorMessage"] = "Bạn không thể tự mua sản phẩm của chính mình!";
            return RedirectToAction("Details", "Product", new { id = productId });
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
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        var product = await _context.Products
                         .Include(p => p.ProductImages) 
                         .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null) return NotFound();

        // BẢO MẬT: Chỉ chủ sở hữu mới được sửa
        if (product.SellerId != userId)
        {
            return RedirectToAction("Index", "Home"); // Hoặc trang báo lỗi 403
        }

        // Load danh mục để đổ vào dropdown
        ViewBag.ParentCategories = new SelectList(await _context.Categories.Where(c => c.ParentId == null).ToListAsync(), "CategoryId", "CategoryName");
        
        // Nếu sản phẩm đang có danh mục con, load danh mục con tương ứng
        if (product.CategoryId != null)
        {
            var currentCategory = await _context.Categories.FindAsync(product.CategoryId);
            if (currentCategory != null && currentCategory.ParentId != null)
            {
                // Nếu category hiện tại là con, thì load list anh em của nó
                ViewBag.ChildCategories = new SelectList(await _context.Categories.Where(c => c.ParentId == currentCategory.ParentId).ToListAsync(), "CategoryId", "CategoryName", product.CategoryId);
                // Set lại ParentId cho View để nó chọn đúng cha
                ViewBag.SelectedParentId = currentCategory.ParentId;
            }
        }

        return View(product);
    }

    // 2. POST: Xử lý lưu thay đổi
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product model, IFormFile[]? newImages)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        if (id != model.ProductId) return NotFound();

        // Load sản phẩm gốc từ DB để cập nhật (không dùng model trực tiếp để tránh hack field khác)
        var productInDb = await _context.Products
                                .Include(p => p.ProductImages)
                                .FirstOrDefaultAsync(p => p.ProductId == id);

        if (productInDb == null || productInDb.SellerId != userId) return Unauthorized();

        if (ModelState.IsValid)
        {
            // Cập nhật thông tin cơ bản
            productInDb.ProductName = model.ProductName;
            productInDb.Description = model.Description;
            productInDb.Price = model.Price;
            productInDb.Quantity = model.Quantity;
            productInDb.IsNew = model.IsNew;
            productInDb.CategoryId = model.CategoryId;
            productInDb.Location = model.Location;
            
            // QUAN TRỌNG: Sửa xong phải chuyển về Pending để Admin duyệt lại
            productInDb.Status = "Pending"; 

            // Xử lý ảnh (Nếu người dùng có upload ảnh mới)
            if (newImages != null && newImages.Length > 0)
            {
                // Cách đơn giản nhất: Xóa ảnh cũ, lưu ảnh mới
                // (Thực tế bạn có thể làm UI phức tạp hơn để xóa từng ảnh lẻ)
                
                // 1. Xóa ảnh cũ trong DB
                _context.ProductImages.RemoveRange(productInDb.ProductImages);
                
                // 2. Thêm ảnh mới
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "products");
                for (int i = 0; i < newImages.Length; i++)
                {
                    var file = newImages[i];
                    if (file.Length > 0)
                    {
                        var fileName = $"{id}_{DateTime.Now.Ticks}_{i}{Path.GetExtension(file.FileName)}";
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        var pImage = new ProductImage
                        {
                            ProductId = id,
                            ImageUrl = $"/images/products/{fileName}",
                            IsMain = (i == 0) // Mặc định cái đầu tiên là ảnh bìa
                        };
                        _context.ProductImages.Add(pImage);
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật thành công! Sản phẩm đang chờ Admin duyệt lại.";
            return RedirectToAction("Details", new { id = id });
        }
        
        // Nếu lỗi thì load lại View
        ViewBag.ParentCategories = new SelectList(await _context.Categories.Where(c => c.ParentId == null).ToListAsync(), "CategoryId", "CategoryName");
        return View(model);
    }

}
