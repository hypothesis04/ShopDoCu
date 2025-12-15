using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopDoCu.Models;
using System.Diagnostics;

namespace ShopDoCu.Controllers
{
    public class HomeController : Controller
    {
        private readonly ChoBanDoCuContext _context;

        public HomeController(ChoBanDoCuContext context)
        {
            _context = context;
        }

        // Trang chủ - hiển thị danh mục, banner quảng cáo và danh sách sản phẩm
        public async Task<IActionResult> Index()
        {
            // Lấy danh sách danh mục chính (không có ParentId) để hiển thị ở đầu trang
            var categories = await _context.Categories
                .Include(c => c.InverseParent) 
                .Where(c => c.ParentId == null) 
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            // 2. LẤY SẢN PHẨM (GIỮ NGUYÊN CODE CŨ CỦA BẠN)
            var products = await _context.Products
                .Where(p => p.Status == "Active" || p.Status == null)
                .Include(p => p.ProductImages)
                .Include(p => p.Category)
                .Include(p => p.Seller)
                .OrderByDescending(p => p.CreatedAt)
                .Take(12)
                .ToListAsync();

            ViewBag.Categories = categories;
            return View(products);
        }
    }


}

