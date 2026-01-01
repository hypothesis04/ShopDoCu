using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopDoCu.Models;
using ShopDoCu.Views.Product;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace ShopDoCu.Controllers;

public class SellerChannelController : Controller
{
    private readonly ChoBanDoCuContext _context;

    public SellerChannelController(ChoBanDoCuContext context)
    {
        _context = context;
    }

    // --- HELPER: Kiểm tra quyền Seller bằng SESSION ---
    private bool IsSeller()
    {
       // 1. Lấy ID người dùng hiện tại
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return false;

        // 2. [QUAN TRỌNG] Truy vấn Database để lấy quyền mới nhất
        // (Không tin tưởng hoàn toàn vào Session cũ)
        var userInDb = _context.Users.FirstOrDefault(u => u.UserId == userId);

        if (userInDb == null) return false;

        // 3. CẬP NHẬT LẠI SESSION (Đồng bộ hóa)
        // Nếu quyền trong DB khác quyền trong Session -> Cập nhật ngay
        var currentSessionRole = HttpContext.Session.GetString("Role");
        if (userInDb.Role != currentSessionRole)
        {
            HttpContext.Session.SetString("Role", userInDb.Role ?? "User");
            
            // Nếu bị khóa nick (Locked) thì cũng coi như không phải Seller
            if (userInDb.IsLocked == true) 
            {
                return false;
            }
        }

        // 4. Trả về kết quả dựa trên dữ liệu thật trong DB
        return userInDb.Role == "Seller";
    }

    // ==========================================
    // 1. DASHBOARD (TRANG CHỦ SHOP)
    // ==========================================
    public async Task<IActionResult> Index()
    {
        if (!IsSeller()) return RedirectToAction("Login", "Account");
        var userId = HttpContext.Session.GetInt32("UserId");

        // Thống kê Doanh thu (Chỉ tính đơn đã hoàn thành)
        var totalRevenue = await _context.Orders
            .Where(o => o.SellerId == userId && o.Status == "Completed")
            .SumAsync(o => o.TotalAmount ?? 0);

        // Tổng đơn hàng
        var totalOrders = await _context.Orders
            .Where(o => o.SellerId == userId)
            .CountAsync();

        // Đơn chờ duyệt
        var pendingOrders = await _context.Orders
            .Where(o => o.SellerId == userId && o.Status == "Pending")
            .CountAsync();

        // Sản phẩm đã bán (Tổng số lượng trong các đơn đã hoàn thành)
        var soldItems = await _context.OrderDetails
            .Include(od => od.Order)
            .Where(od => od.Order.SellerId == userId && od.Order.Status == "Completed")
            .SumAsync(od => od.Quantity ?? 0);

        ViewBag.TotalRevenue = totalRevenue;
        ViewBag.TotalOrders = totalOrders;
        ViewBag.PendingOrders = pendingOrders;
        ViewBag.SoldItems = soldItems;

        return View();
    }

    // ==========================================
    // 2. QUẢN LÝ ĐƠN HÀNG (CÓ TAB LỌC)
    // ==========================================
    public async Task<IActionResult> ManageOrders(string status = "All")
    {
        if (!IsSeller()) return RedirectToAction("Login", "Account");
        var userId = HttpContext.Session.GetInt32("UserId");

        var query = _context.Orders
            .Include(o => o.OrderDetails).ThenInclude(od => od.Product).ThenInclude(p => p.ProductImages)
            .Include(o => o.User) // Lấy thông tin khách
            .Where(o => o.SellerId == userId);

        // Lọc theo Tab trạng thái
        if (status != "All")
        {
            query = query.Where(o => o.Status == status);
        }

        // Đếm số lượng để hiện Badge đỏ trên Menu
        ViewBag.CountPending = await _context.Orders.CountAsync(o => o.SellerId == userId && o.Status == "Pending");
        ViewBag.CountShipping = await _context.Orders.CountAsync(o => o.SellerId == userId && o.Status == "Shipping");
        ViewBag.CountReturn = await _context.Orders.CountAsync(o => o.SellerId == userId && o.Status == "ReturnRequested");

        ViewBag.CurrentStatus = status;

        return View(await query.OrderByDescending(o => o.OrderDate).ToListAsync());
    }

    // ==========================================
    // 3. CHI TIẾT ĐƠN HÀNG
    // ==========================================
    public async Task<IActionResult> OrderDetails(int id)
    {
        if (!IsSeller()) return RedirectToAction("Login", "Account");
        var userId = HttpContext.Session.GetInt32("UserId");

        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Product).ThenInclude(p => p.ProductImages)
            .FirstOrDefaultAsync(o => o.OrderId == id && o.SellerId == userId);

        if (order == null) return NotFound();

        return View(order);
    }

    // ==========================================
    // 4. XỬ LÝ TRẠNG THÁI (DUYỆT, HỦY, GIAO...)
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(int id, string status)
    {
        if (!IsSeller()) return RedirectToAction("Login", "Account");
        var userId = HttpContext.Session.GetInt32("UserId");

        var order = await _context.Orders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.OrderId == id && o.SellerId == userId);

        if (order != null)
        {
            // Chặn sửa nếu đơn đã kết thúc (trừ trường hợp từ chối trả hàng)
            if ((order.Status == "Cancelled" || order.Status == "Returned" || order.Status == "Completed") && status != "ReturnRejected")
            {
                TempData["ErrorMessage"] = "Đơn hàng này đã kết thúc, không thể chỉnh sửa!";
                return RedirectToAction("OrderDetails", new { id = id });
            }

            // LOGIC HOÀN KHO (Cộng lại số lượng sản phẩm)
            if (status == "Cancelled" || status == "Returned")
            {
                foreach (var item in order.OrderDetails)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null) product.Quantity += item.Quantity; 
                }
                TempData["SuccessMessage"] = (status == "Returned") ? "Đã nhận lại hàng trả & hoàn kho!" : "Đã hủy đơn & hoàn kho!";
            }
            // LOGIC TỪ CHỐI TRẢ HÀNG -> Quay về Completed
            else if (status == "ReturnRejected")
            {
                status = "Completed";
                TempData["ErrorMessage"] = "Đã từ chối yêu cầu trả hàng.";
            }
            else if (status == "Shipping") TempData["SuccessMessage"] = "Đã xác nhận giao hàng!";
            else if (status == "Completed") TempData["SuccessMessage"] = "Đơn hàng hoàn tất!";

            order.Status = status;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("OrderDetails", new { id = id });
    }

    // ==========================================
    // 5. QUẢN LÝ SẢN PHẨM (MỚI BỔ SUNG)
    // ==========================================
    public async Task<IActionResult> MyProducts(string search, string status, int? categoryId)
    {
        if (!IsSeller()) return RedirectToAction("Login", "Account");
        var userId = HttpContext.Session.GetInt32("UserId");

        // Query sản phẩm kèm Ảnh thumbnail
        var productsQuery = _context.Products
            .Include(p => p.Category)
            .Include(p => p.ProductImages) 
            .Where(p => p.SellerId == userId);

        // Tìm kiếm & Lọc
        if (!string.IsNullOrWhiteSpace(search))
            productsQuery = productsQuery.Where(p => p.ProductName.Contains(search));

        if (!string.IsNullOrWhiteSpace(status))
            productsQuery = productsQuery.Where(p => p.Status == status);

        if (categoryId.HasValue)
            productsQuery = productsQuery.Where(p => p.CategoryId == categoryId);

        var products = await productsQuery.OrderByDescending(p => p.CreatedAt).ToListAsync();

        // Lấy danh mục cho Sidebar
        ViewBag.Categories = await _context.Categories
            .Where(c => c.ParentId == null && c.Products.Any(p => p.SellerId == userId))
            .ToListAsync();

        // Lưu trạng thái lọc
        ViewBag.SelectedCategoryId = categoryId;
        ViewBag.Search = search;
        ViewBag.Status = status;

        return View(products);
    }

    // ==========================================
    // 6. VÍ TIỀN & LỊCH SỬ GIAO DỊCH
    // ==========================================
    public async Task<IActionResult> MyWallet()
    {
        if (!IsSeller()) return RedirectToAction("Login", "Account");
        var userId = HttpContext.Session.GetInt32("UserId");

        // Tính số dư (Tổng tiền các đơn đã Completed)
        var balance = await _context.Orders
            .Where(o => o.SellerId == userId && o.Status == "Completed")
            .SumAsync(o => o.TotalAmount ?? 0);

        // Lịch sử dùng Coupon (Hoặc sau này là lịch sử rút tiền)
        var transactions = await _context.CouponUsages
            .Where(cu => cu.UserId == userId)
            .OrderByDescending(cu => cu.UsedAt)
            .ToListAsync();

        ViewBag.Balance = balance;
        return View(transactions);
    }

    [HttpGet]
    public IActionResult CreateProduct()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            // Chưa đăng nhập -> Về trang Login
            return RedirectToAction("Login", "Account");
        }

        // 2. Đã đăng nhập nhưng KHÔNG phải là Seller
        if (!IsSeller()) 
        {
            // Có thể thêm thông báo nhỏ nếu muốn
            TempData["ErrorMessage"] = "Bạn cần đăng ký bán hàng trước khi đăng sản phẩm!";
            // Chuyển hướng tới form đăng ký bán hàng (trong AccountController)
            return RedirectToAction("SellerRegistration", "Account");
        }

        // Lấy danh mục cha để đổ vào dropdown
        ViewBag.ParentCategories = _context.Categories
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.CategoryName)
            .ToList();

        return View();
    }

    // POST: Xử lý lưu (Trả về JSON cho AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(ProductCreateViewModel model)
    {
        if (!IsSeller()) return Json(new { success = false, message = "Phiên đăng nhập hết hạn." });

        var userId = HttpContext.Session.GetInt32("UserId");
        
        // 1. Lấy file ảnh từ Request
        var imageFiles = Request.Form.Files.Where(f => f.Name == "ProductImages").ToList();

        // 2. Validate thủ công
        if (imageFiles == null || imageFiles.Count < 3)
            return Json(new { success = false, message = "Vui lòng chọn ít nhất 3 ảnh sản phẩm." });

        if (!ModelState.IsValid)
        {
            // Lấy lỗi đầu tiên để báo ra
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return Json(new { success = false, message = firstError ?? "Dữ liệu không hợp lệ." });
        }

        // 3. BẮT ĐẦU TRANSACTION 
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // Lưu Product
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
                    Status = "Pending", // Chờ duyệt
                    CreatedAt = DateTime.UtcNow,
                    Views = 0,
                    Specifications = model.Specifications // Lưu thông số kỹ thuật JSON
                };
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // Lưu Ảnh
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
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
                await transaction.CommitAsync();

                // TRẢ VỀ JSON THÀNH CÔNG
                return Json(new { success = true, message = "Đăng bán thành công! Sản phẩm đang chờ duyệt." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }
    [HttpGet]
    public async Task<IActionResult> EditProduct(int id)
    {
        if (!IsSeller()) return RedirectToAction("Login", "Account");
        var userId = HttpContext.Session.GetInt32("UserId");

        var product = await _context.Products
            .Include(p => p.ProductImages)
            .FirstOrDefaultAsync(p => p.ProductId == id && p.SellerId == userId);

        if (product == null) return NotFound();

        // Load danh mục cha
        ViewBag.ParentCategories = new SelectList(await _context.Categories.Where(c => c.ParentId == null).ToListAsync(), "CategoryId", "CategoryName");

        // Load danh mục con (nếu có)
        if (product.CategoryId != null)
        {
            var currentCategory = await _context.Categories.FindAsync(product.CategoryId);
            if (currentCategory != null && currentCategory.ParentId != null)
            {
                ViewBag.ChildCategories = new SelectList(await _context.Categories.Where(c => c.ParentId == currentCategory.ParentId).ToListAsync(), "CategoryId", "CategoryName", product.CategoryId);
                ViewBag.SelectedParentId = currentCategory.ParentId;
            }
        }

        return View(product);
    }

    // POST: Xử lý sửa (Trả về JSON)
    [HttpPost]
    [ValidateAntiForgeryToken]

    public async Task<IActionResult> EditProduct(int id, Product model, IFormFile[]? newImages, int MainImageIndex = 0) 
    {
        if (!IsSeller()) return Json(new { success = false, message = "Hết phiên đăng nhập." });
        
        var userId = HttpContext.Session.GetInt32("UserId");
        var productInDb = await _context.Products.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.ProductId == id);

        if (productInDb == null || productInDb.SellerId != userId) 
            return Json(new { success = false, message = "Không tìm thấy sản phẩm hoặc không có quyền." });

        try 
        {
            // 1. Cập nhật thông tin cơ bản
            productInDb.ProductName = model.ProductName;
            productInDb.Description = model.Description;
            productInDb.Price = model.Price;
            productInDb.Quantity = model.Quantity;
            productInDb.IsNew = model.IsNew;
            productInDb.CategoryId = model.CategoryId;
            productInDb.Location = model.Location;
            productInDb.Specifications = model.Specifications; 
            productInDb.Status = "Pending"; // Sửa xong phải chờ duyệt lại

            // 2. Xử lý ảnh mới (Nếu có upload)
            if (newImages != null && newImages.Length > 0)
            {
                // Xóa ảnh cũ
                _context.ProductImages.RemoveRange(productInDb.ProductImages);
                
                // Thêm ảnh mới
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
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
                    
                        _context.ProductImages.Add(new ProductImage { 
                            ProductId = id, 
                            ImageUrl = $"/images/products/{fileName}", 
                            IsMain = (i == MainImageIndex) // <-- SỬA CHỖ NÀY
                        });
                    }
                }
            }
            
            await _context.SaveChangesAsync();
            return Json(new { success = true, title = "Cập nhật thành công!", message = "Sản phẩm đã được gửi đi chờ duyệt lại." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Lỗi: " + ex.Message });
        }
    }
}