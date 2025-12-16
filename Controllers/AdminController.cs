using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopDoCu.Models;

namespace ShopDoCu.Controllers;

// Controller quản lý trang admin - chỉ admin mới truy cập được

// Controller quản trị dành cho Admin
// Tất cả các chức năng trong controller này chỉ dành cho tài khoản có quyền Admin
public class AdminController : Controller
{
    // DbContext để thao tác dữ liệu với database (Entity Framework)
    private readonly ChoBanDoCuContext _context;

    // Inject DbContext qua constructor để sử dụng trong các action
    public AdminController(ChoBanDoCuContext context)
    {
        _context = context;
    }

    // Kiểm tra tài khoản hiện tại có phải admin không
    // Trả về true nếu session có Role là "Admin"
    private bool IsAdmin()
    {
        var role = HttpContext.Session.GetString("Role");
        return role == "Admin";
    }

    // Dashboard
    // Trang dashboard admin: thống kê tổng quan
    // Hiển thị số lượng người dùng, sản phẩm, đơn hàng, danh mục, sản phẩm chờ duyệt
    public async Task<IActionResult> Index()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");
        var totalUsers = await _context.Users.CountAsync();
        var totalProducts = await _context.Products.CountAsync();
        var totalOrders = await _context.Orders.CountAsync();
        var totalCategories = await _context.Categories.CountAsync();
        var pendingProducts = await _context.Products.CountAsync(p => p.Status == "Pending");
        var pendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending");
        var PendingSellers = await _context.Users.CountAsync(u => u.Role == "SellerPending");
        ViewBag.TotalUsers = totalUsers;
        ViewBag.TotalProducts = totalProducts;
        ViewBag.TotalOrders = totalOrders;
        ViewBag.TotalCategories = totalCategories;
        ViewBag.PendingProducts = pendingProducts;
        ViewBag.PendingOrders = pendingOrders;
        ViewBag.PendingSellers = PendingSellers;
        return View();
    }

    // Danh sách sản phẩm
    // Danh sách sản phẩm, có thể lọc theo trạng thái (Active, Pending, Locked...)
    // Admin chỉ có quyền duyệt, xoá, khoá/mở, xem chi tiết sản phẩm
   public async Task<IActionResult> Products(string searchString)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        var products = _context.Products
            .Include(p => p.Seller)
            .Include(p => p.Category)
            .AsQueryable();

        // LOGIC TÌM KIẾM
        if (!string.IsNullOrEmpty(searchString))
        {
            // Tìm theo tên sản phẩm hoặc tên người bán
            products = products.Where(p => p.ProductName.Contains(searchString) || p.Seller.UserName.Contains(searchString));
        }

        ViewBag.SearchString = searchString; // Lưu từ khóa để hiện lại trên View
        return View(await products.OrderByDescending(p => p.CreatedAt).ToListAsync());
    }
    // Xem chi tiết sản phẩm: thông tin, người bán, danh mục, trạng thái
    // Xem chi tiết sản phẩm
   public async Task<IActionResult> Details(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");
        
        var product = await _context.Products
            .Include(p => p.Seller)
            .Include(p => p.Category)
            .Include(p => p.ProductImages) 
            .FirstOrDefaultAsync(p => p.ProductId == id);
            
        if (product == null) return NotFound();
        return View(product);
    }
    // Khoá sản phẩm
    // Khoá sản phẩm (chuyển trạng thái sang Locked)
    // Sản phẩm bị khoá sẽ không hiển thị cho người dùng thường
    public async Task<IActionResult> LockProduct(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            product.Status = "Locked";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã khoá sản phẩm!";
        }
        return RedirectToAction(nameof(Products));
    }

    // 6. Mở khoá sản phẩm (Bị khoá -> Đang bán)
    public async Task<IActionResult> UnlockProduct(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            product.Status = "Active";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã mở khoá sản phẩm!";
        }
        return RedirectToAction(nameof(Products));
    }

 // 8. Xoá sản phẩm vĩnh viễn (Delete)
    public async Task<IActionResult> DeleteProduct(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        // BƯỚC 1: Load sản phẩm kèm theo ảnh
        var product = await _context.Products
            .Include(p => p.ProductImages)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product != null)
        {
            // BƯỚC 2: Xóa file ảnh vật lý (Copy y hệt đoạn trên)
            foreach (var image in product.ProductImages)
            {
                if (!string.IsNullOrEmpty(image.ImageUrl))
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", image.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
            }

            // BƯỚC 3: Xóa dữ liệu ảnh trong bảng ProductImages
            _context.ProductImages.RemoveRange(product.ProductImages);

            // BƯỚC 4: Xóa sản phẩm
            _context.Products.Remove(product);
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa sản phẩm!";
        }
        return RedirectToAction(nameof(Products));
    }
    // Danh sách người dùng
    // Admin có thể khoá/mở, xoá, xem chi tiết, duyệt/từ chối seller
    // Danh sách người dùng
   public async Task<IActionResult> Users(string searchString)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");
        
        var users = _context.Users.AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            // Tìm theo Username, Email hoặc SĐT
            users = users.Where(u => u.UserName.Contains(searchString) || u.Email.Contains(searchString) || u.Phone.Contains(searchString));
        }

        ViewBag.SearchString = searchString;
        return View(await users.ToListAsync());
    }

    // Xem chi tiết người dùng: thông tin cá nhân, trạng thái, vai trò, ngày tạo
    // Xem chi tiết người dùng
    public async Task<IActionResult> UserDetails(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        return View(user);
    }

    // Khoá tài khoản (GET: hiển thị form nhập lý do)
    // Hiển thị form khoá tài khoản (GET)
    // Admin nhập lý do khoá tài khoản
    [HttpGet]
    public async Task<IActionResult> LockUser(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        return View(user);
    }

    // Khoá tài khoản (POST: nhận lý do khoá)
    // Xử lý khoá tài khoản (POST), nhận lý do khoá
    // Cập nhật trạng thái IsLocked, thời gian và lý do khoá
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LockUser(int id, string lockReason)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.IsLocked = true;
        user.LockedAt = DateTime.Now;
        user.LockReason = lockReason;
        await _context.SaveChangesAsync();
        return RedirectToAction("Users");
    }

    // Mở khoá tài khoản
    // Mở khoá tài khoản người dùng
    // Đặt lại trạng thái hoạt động bình thường
    public async Task<IActionResult> UnlockUser(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.IsLocked = false;
        user.LockedAt = null;
        user.LockReason = null;
        await _context.SaveChangesAsync();
        return RedirectToAction("Users");
    }

    // Xoá người dùng
    // Xoá người dùng khỏi hệ thống
    // Hành động này không thể hoàn tác
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return RedirectToAction("Users");
    }
    // Xử lý Duyệt người bán
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveSeller(int id)
    {
        // Kiểm tra quyền Admin
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Chuyển vai trò thành Seller (Người bán)
        user.Role = "Seller";
        
        await _context.SaveChangesAsync();

        // Quay lại trang danh sách người dùng
        return RedirectToAction("Users");
    }

    // Xử lý Từ chối người bán
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectSeller(int id)
    {
        // Kiểm tra quyền Admin
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Cách 1: Xoá luôn tài khoản đăng ký này (theo nút "Xoá" ở giao diện cũ)
        _context.Users.Remove(user);

        // Cách 2: (Nếu bạn muốn giữ tài khoản nhưng huỷ yêu cầu bán) thì dùng dòng dưới và bỏ dòng Remove đi:
        // user.Role = "User"; 

        await _context.SaveChangesAsync();

        return RedirectToAction("Users");
    }

  

    // 2. Cập nhật lại hàm ApproveProduct (Duyệt)
    // Để sau khi duyệt xong thì quay lại danh sách chờ (PendingProducts) thay vì danh sách tất cả
    public async Task<IActionResult> ApproveProduct(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            product.Status = "Active"; // Chuyển sang trạng thái hoạt động
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã duyệt sản phẩm thành công!";
        }
        
        // Quay lại trang danh sách chung
        return RedirectToAction(nameof(Products)); 
    }

    // 3. Hàm Từ chối sản phẩm (Reject) 
    // POST: Admin/RejectProduct/5
   public async Task<IActionResult> RejectProduct(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        // BƯỚC 1: Phải Include ProductImages để lấy danh sách ảnh cần xóa
        var product = await _context.Products
            .Include(p => p.ProductImages) 
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product != null)
        {
            // BƯỚC 2: (Tùy chọn) Xóa file ảnh vật lý trong thư mục wwwroot để đỡ tốn dung lượng
            foreach (var image in product.ProductImages)
            {
                if (!string.IsNullOrEmpty(image.ImageUrl))
                {
                    // Chuyển đường dẫn web (/images/...) thành đường dẫn ổ cứng (C:\...)
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", image.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
            }

            // BƯỚC 3: Xóa dữ liệu ảnh trong Database trước
            _context.ProductImages.RemoveRange(product.ProductImages);

            // BƯỚC 4: Sau đó mới xóa Sản phẩm
            _context.Products.Remove(product);
            
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã từ chối và xóa bài đăng!";
        }
        
        return RedirectToAction(nameof(Products));
    }
    // 1. Danh sách đơn hàng
    public async Task<IActionResult> Orders(string searchString)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        var orders = _context.Orders
            .Include(o => o.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            // Tìm theo Mã đơn (ID) hoặc Tên người đặt
            // Lưu ý: search ID cần chuyển đổi số hoặc so sánh chuỗi
            orders = orders.Where(o => o.OrderId.ToString().Contains(searchString) || o.User.UserName.Contains(searchString));
        }

        ViewBag.SearchString = searchString;
        return View(await orders.OrderByDescending(o => o.OrderDate).ToListAsync());
    }

    // 2. Xem chi tiết đơn hàng & Xử lý
    public async Task<IActionResult> OrderDetails(int id)
    {
      if (!IsAdmin()) return RedirectToAction("Login", "Account");
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .ThenInclude(p => p.ProductImages)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        // Đảm bảo không bị null khi truy cập ProductImages
        if (order?.OrderDetails != null)
        {
            foreach (var od in order.OrderDetails)
            {
                if (od.Product != null && od.Product.ProductImages == null)
                {
                    od.Product.ProductImages = new List<ProductImage>();
                }
            }
        }

        if (order == null) return NotFound();
        return View(order);
    }

    // 3. Cập nhật trạng thái đơn hàng (Duyệt, Giao, Hủy...)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(int id, string status)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");
        // Lấy đơn hàng kèm chi tiết để biết số lượng từng món
        var order = await _context.Orders
            .Include(o => o.OrderDetails) 
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order != null)
        {
            // Check 1: Nếu đơn đã xong/hủy rồi thì không cho sửa lung tung nữa
            if (order.Status == "Cancelled" || order.Status == "Returned")
            {
                TempData["ErrorMessage"] = "Đơn hàng này đã kết thúc quy trình, không thể thay đổi trạng thái!";
                return RedirectToAction("OrderDetails", new { id = id });
            }

            // --- LOGIC 1: HỦY ĐƠN HOẶC CHẤP NHẬN TRẢ HÀNG (HOÀN KHO) ---
            // Nếu Admin chọn "Cancelled" (Hủy) hoặc "Returned" (Đã nhận hàng trả)
            if (status == "Cancelled" || status == "Returned")
            {
                foreach (var item in order.OrderDetails)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Quantity += item.Quantity; // CỘNG LẠI KHO
                    }
                }

                if (status == "Returned")
                {
                    // Nếu trả hàng thành công -> Cập nhật trạng thái tiền là "Đã hoàn tiền"
                    order.PaymentStatus = "Refunded";
                    TempData["SuccessMessage"] = $"Đã xác nhận trả hàng đơn #{id}. Kho đã được cập nhật!";
                }
                else 
                {
                    TempData["SuccessMessage"] = $"Đã hủy đơn hàng #{id} và hoàn lại kho thành công!";
                }
            }
            // --- LOGIC 2: ĐANG GIAO HÀNG ---
            else if (status == "Shipping")
            {
                TempData["SuccessMessage"] = $"Đơn hàng #{id} đang được giao!";
            }
            // --- LOGIC 3: TỪ CHỐI TRẢ HÀNG (QUAY VỀ COMPLETED) ---
            else if (status == "ReturnRejected")
            {
                // Nếu từ chối, ta đẩy trạng thái về lại Completed (Hoàn thành) như cũ
                status = "Completed"; 
                TempData["ErrorMessage"] = "Đã từ chối yêu cầu trả hàng.";
            }
            
            // Lưu xuống DB
            order.Status = status;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("OrderDetails", new { id = id });
       
    }
   // --- QUẢN LÝ DANH MỤC (CATEGORY) ---

    // 1. Danh sách danh mục
    public async Task<IActionResult> Categories(string searchString)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        var categories = _context.Categories
            .Include(c => c.Parent)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            categories = categories.Where(c => c.CategoryName.Contains(searchString));
        }

        ViewBag.SearchString = searchString;
        return View(await categories.OrderBy(c => c.ParentId).ThenBy(c => c.CategoryId).ToListAsync());
    }

    // 2. Tạo danh mục mới (GET)
    public IActionResult CreateCategory()
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        // Lấy danh sách các danh mục Cha (ParentId = null) để đổ vào Combobox
        ViewBag.ParentCategories = _context.Categories
            .Where(c => c.ParentId == null)
            .ToList();

        return View();
    }

    // 2. Tạo danh mục mới (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(Category category)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        if (ModelState.IsValid)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Thêm danh mục thành công!";
            return RedirectToAction(nameof(Categories));
        }
        
        // Nếu lỗi, load lại dropdown
        ViewBag.ParentCategories = _context.Categories.Where(c => c.ParentId == null).ToList();
        return View(category);
    }

    // 3. Sửa danh mục (GET)
    public async Task<IActionResult> EditCategory(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound();

        // Load danh mục cha, TRỪ chính nó ra (không thể chọn chính mình làm cha)
        ViewBag.ParentCategories = _context.Categories
            .Where(c => c.ParentId == null && c.CategoryId != id)
            .ToList();

        return View(category);
    }

    // 3. Sửa danh mục (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, Category category)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        if (id != category.CategoryId) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Categories.Any(e => e.CategoryId == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Categories));
        }

        ViewBag.ParentCategories = _context.Categories
            .Where(c => c.ParentId == null && c.CategoryId != id)
            .ToList();
        return View(category);
    }

    // 4. Xóa danh mục
    public async Task<IActionResult> DeleteCategory(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        var category = await _context.Categories
            .Include(c => c.InverseParent) // Các danh mục con
            .Include(c => c.Products)      // Các sản phẩm thuộc danh mục này
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category == null) return NotFound();

        // KIỂM TRA RÀNG BUỘC DỮ LIỆU
        if (category.Products.Any())
        {
            TempData["ErrorMessage"] = "Không thể xóa! Danh mục này đang chứa sản phẩm.";
            return RedirectToAction(nameof(Categories));
        }

        if (category.InverseParent.Any())
        {
            TempData["ErrorMessage"] = "Không thể xóa! Danh mục này đang chứa danh mục con.";
            return RedirectToAction(nameof(Categories));
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã xóa danh mục!";
        
        return RedirectToAction(nameof(Categories));
    }
    // Danh sách Coupon (Có tìm kiếm)
    public async Task<IActionResult> Coupons(string searchString)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        var coupons = _context.Coupons.AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            coupons = coupons.Where(c => c.Code.Contains(searchString));
        }

        ViewBag.SearchString = searchString;
        return View(await coupons.OrderByDescending(c => c.CouponId).ToListAsync());
    }
    public IActionResult CreateCoupon()
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");
        return View();
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCoupon(Coupon coupon)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");

        // Kiểm tra trùng mã
        if (await _context.Coupons.AnyAsync(c => c.Code == coupon.Code))
        {
            ModelState.AddModelError("Code", "Mã giảm giá này đã tồn tại!");
            return View(coupon);
        }

        if (ModelState.IsValid)
        {
            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Tạo mã giảm giá thành công!";
            return RedirectToAction(nameof(Coupons));
        }
        return View(coupon);
    }
    public async Task<IActionResult> EditCoupon(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");
        var coupon = await _context.Coupons.FindAsync(id);
        if (coupon == null) return NotFound();
        return View(coupon);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCoupon(int id, Coupon coupon)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");
        if (id != coupon.CouponId) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(coupon);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật mã giảm giá thành công!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Coupons.Any(e => e.CouponId == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Coupons));
        }
        return View(coupon);
    }
    public async Task<IActionResult> DeleteCoupon(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Account");
        var coupon = await _context.Coupons.FindAsync(id);
        if (coupon != null)
        {
            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa mã giảm giá!";
        }
        return RedirectToAction(nameof(Coupons));
    }
}


