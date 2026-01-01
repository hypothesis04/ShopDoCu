using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopDoCu.Models;

namespace ShopDoCu.Controllers;

public class UserCouponController : Controller
{
    private readonly ChoBanDoCuContext _context;

    public UserCouponController(ChoBanDoCuContext context)
    {
        _context = context;
    }

    // Trang danh sách mã giảm giá của tôi
    public async Task<IActionResult> Index()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Account");

        // 1. Lấy danh sách mã trong ví người dùng
        var myCoupons = await _context.UserCoupons
            .Where(u => u.UserId == userId)
            .OrderByDescending(u => u.UserCouponId)
            .ToListAsync();

        // 2. Lấy thêm thông tin chi tiết (Hạn dùng, Đơn tối thiểu) từ bảng Coupon gốc
        // (Vì bảng UserCoupon hiện tại chỉ lưu Code và Số tiền giảm)
        var codes = myCoupons.Select(u => u.Code).Distinct().ToList();
        
        var masterCoupons = await _context.Coupons
            .Where(c => codes.Contains(c.Code))
            .ToDictionaryAsync(c => c.Code, c => c);

        // Truyền dữ liệu bổ sung sang View
        ViewBag.MasterCoupons = masterCoupons;

        return View(myCoupons);
    }
}