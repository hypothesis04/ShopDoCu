using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopDoCu.Models;
using ShopDoCu.Views.Account;
using System.IO;

namespace ShopDoCu.Controllers;

public class AccountController : Controller
{
    private readonly ChoBanDoCuContext _context;

    public AccountController(ChoBanDoCuContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 1. Lấy user từ DB
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == model.UserName);

        // 2. Kiểm tra tài khoản và mật khẩu
        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng");
            return View(model);
        }

        // --- KIỂM TRA KHÓA TÀI KHOẢN ---
        if (user.IsLocked == true)
        {
            // Lấy lý do khóa (nếu null thì ghi mặc định)
            string reason = user.LockReason ?? "Vi phạm chính sách cộng đồng.";
            
            // Báo lỗi ra màn hình
            ModelState.AddModelError(string.Empty, "⛔ Tài khoản của bạn đã bị khóa!");
            ModelState.AddModelError(string.Empty, $"Ngày khoá: {user.LockedAt?.ToString("dd/MM/yyyy HH:mm") ?? "Không rõ"}");
            ModelState.AddModelError(string.Empty, $"Lý do: {reason}");
            // Trả về view luôn, KHÔNG cho chạy xuống hàm SignIn
            return View(model);
        }
        // -----------------------------------------------

        // 3. Nếu không bị khóa thì mới cho đăng nhập
        SignIn(user);

        if (user.Role == "Admin")
        {
            return RedirectToAction("Index", "Admin");
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Chuẩn hóa username để tránh khoảng trắng thừa
        var normalizedUserName = model.UserName.Trim();

        // Kiểm tra trùng username trước khi tạo
        if (await _context.Users.AnyAsync(u => u.UserName == normalizedUserName))
        {
            ModelState.AddModelError(nameof(model.UserName), "Tên đăng nhập đã tồn tại");
            return View(model);
        }

        // Tạo avatar chữ cái đầu tiên của tên (ví dụ: "Trường" -> "T")
        var firstLetter = !string.IsNullOrWhiteSpace(model.FullName) 
            ? model.FullName.Trim().Substring(0, 1).ToUpper() 
            : normalizedUserName.Substring(0, 1).ToUpper();
        // Tạo URL avatar dạng SVG với chữ cái (sẽ dùng data URI hoặc tạo file)
        var avatarUrl = GenerateAvatarUrl(firstLetter);

        // Tạo bản ghi User mới với mật khẩu đã hash
        var newUser = new User
        {
            UserName = normalizedUserName,
            FullName = model.FullName.Trim(),
            Email = null, // Email sẽ nhập ở form cập nhật thông tin
            Phone = model.Phone,
            Address = model.Address?.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            AvatarUrl = avatarUrl, // Avatar là chữ cái
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        SignIn(newUser);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    // Trang cập nhật thông tin cá nhân
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return RedirectToAction("Login");
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(User model, IFormFile? avatarFile)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return RedirectToAction("Login");
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        // Cập nhật thông tin
        user.FullName = model.FullName;
        user.Email = model.Email;
        user.Phone = model.Phone;
        user.Address = model.Address;

        // Xử lý upload avatar nếu có
        if (avatarFile != null && avatarFile.Length > 0)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "avatars");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"{user.UserId}_{DateTime.UtcNow.Ticks}{Path.GetExtension(avatarFile.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await avatarFile.CopyToAsync(stream);
            }
            user.AvatarUrl = $"/images/avatars/{fileName}";
        }
        // Nếu không có avatar và chưa có avatarUrl, tạo avatar chữ cái
        else if (string.IsNullOrEmpty(user.AvatarUrl))
        {
            var firstLetter = !string.IsNullOrWhiteSpace(user.FullName) 
                ? user.FullName.Trim().Substring(0, 1).ToUpper() 
                : user.UserName.Substring(0, 1).ToUpper();
            user.AvatarUrl = GenerateAvatarUrl(firstLetter);
        }

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Cập nhật session
        SignIn(user);

        TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
        return RedirectToAction("Profile");
    }

    // Hàm tạo URL avatar từ chữ cái (dùng data URI SVG)
    private string GenerateAvatarUrl(string letter)
    {
        // Tạo SVG avatar với chữ cái, màu nền ngẫu nhiên dựa trên chữ cái
        var colors = new[] { "#0d6efd", "#198754", "#dc3545", "#ffc107", "#0dcaf0", "#6f42c1", "#fd7e14" };
        var colorIndex = letter[0] % colors.Length;
        var bgColor = colors[colorIndex];

        var svg = $@"<svg width='100' height='100' xmlns='http://www.w3.org/2000/svg'>
            <rect width='100' height='100' fill='{bgColor}'/>
            <text x='50' y='50' font-size='40' font-weight='bold' fill='white' text-anchor='middle' dominant-baseline='central' font-family='Arial'>{letter}</text>
        </svg>";
        
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
        return $"data:image/svg+xml;base64,{base64}";
    }

    // HIỂN THỊ FORM ĐĂNG KÝ BÁN HÀNG (GET)
    [HttpGet]
    public async Task<IActionResult> SellerRegistration()
    {
        // Luôn phải kiểm tra người dùng đã đăng nhập hay chưa
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            // Nếu chưa, chuyển hướng đến trang đăng nhập
            return RedirectToAction("Login");
        }

        // Lấy thông tin đầy đủ của người dùng từ database
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            // Trường hợp hiếm gặp: có session nhưng không có user trong DB
            return RedirectToAction("Login");
        }

        // KIỂM TRA VAI TRÒ (ROLE) CỦA NGƯỜI DÙNG
        // 1. Nếu đã là người bán (Seller)
        if (user.Role == "Seller")
        {
            // Chuyển thẳng sang trang quản lý cửa hàng hoặc đăng sản phẩm
            // Tạm thời chuyển đến trang đăng bán
            return RedirectToAction("Create", "Product");
        }

        // 2. Nếu là Admin
        if (user.Role == "Admin")
        {
            // Admin không có chức năng này, thông báo và về trang chủ
            TempData["ErrorMessage"] = "Tài khoản Quản trị viên không thể đăng ký bán hàng.";
            return RedirectToAction("Index", "Home");
        }

        // 3. Nếu đã gửi yêu cầu và đang chờ duyệt (SellerPending)
        if (user.Role == "SellerPending")
        {
            // Đánh dấu để View biết và hiển thị thông báo "Đang chờ duyệt"
            ViewBag.IsPending = true;
        }
        else
        {
            ViewBag.IsPending = false;
        }

        // Chuẩn bị dữ liệu cho form (lấy từ thông tin sẵn có của user)
        var vm = new SellerRegistrationViewModel
        {
            FullName = user.FullName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Phone = user.Phone ?? string.Empty,
            Address = user.Address ?? string.Empty,
            StoreName = user.StoreName ?? string.Empty,
            PaymentInfo = user.PaymentInfo ?? string.Empty
        };

        // Trả về View với model đã chuẩn bị
        return View(vm);
    }

    // XỬ LÝ KHI NGƯỜI DÙNG NHẤN NÚT ĐĂNG KÝ (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SellerRegistration(SellerRegistrationViewModel model)
    {
        // Luôn phải kiểm tra người dùng đã đăng nhập hay chưa
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            // Nếu session hết hạn giữa chừng, trả về lỗi cho AJAX
            return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn. Vui lòng tải lại trang và đăng nhập." });
        }

        // Kiểm tra xem người dùng có tick vào ô "đồng ý điều khoản" không
        if (!model.AgreeToTerms)
        {
            ModelState.AddModelError(nameof(model.AgreeToTerms), "Bạn phải đồng ý với điều khoản để đăng ký.");
        }

        // Nếu dữ liệu form không hợp lệ
        if (!ModelState.IsValid)
        {
            // Lấy danh sách lỗi và trả về cho AJAX
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = "Dữ liệu không hợp lệ.", errors = errors });
        }

        // Lấy thông tin người dùng từ database
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return Json(new { success = false, message = "Không tìm thấy người dùng." });
        }

        // Cập nhật thông tin người dùng từ form
        user.FullName = model.FullName;
        user.Email = model.Email;
        user.Phone = model.Phone;
        user.Address = model.Address;
        user.StoreName = model.StoreName;
        user.PaymentInfo = model.PaymentInfo;
        // ĐÂY LÀ THAY ĐỔI QUAN TRỌNG: Chuyển vai trò thành "SellerPending" để chờ admin duyệt
        user.Role = "SellerPending"; 

        await _context.SaveChangesAsync();

        // Cập nhật lại thông tin trong Session
        SignIn(user);

        // THAY ĐỔI LỚN: Thay vì Redirect, trả về JSON cho AJAX xử lý
        return Json(new { success = true, message = "Đã gửi đăng ký thành công! Vui lòng chờ xét duyệt." });
    }

    private void SignIn(User user)
    {
        // Lưu thông tin phiên để dùng ở layout (tên, avatar, quyền)
        HttpContext.Session.SetString("UserName", user.UserName);
        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("AvatarUrl", user.AvatarUrl ?? "/images/default-avatar.png");
        HttpContext.Session.SetString("Role", user.Role ?? "User");
    }
}

