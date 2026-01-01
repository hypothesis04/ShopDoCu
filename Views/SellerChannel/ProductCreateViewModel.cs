using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ShopDoCu.Views.Product;

// ViewModel cho form đăng bán sản phẩm - chứa thông tin sản phẩm và ảnh
public class ProductCreateViewModel
{
    [Required(ErrorMessage = "Tên sản phẩm không được bỏ trống")]
    [StringLength(150, ErrorMessage = "Tên sản phẩm tối đa 150 ký tự")]
    [Display(Name = "Tên sản phẩm")]
    public string ProductName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mô tả sản phẩm không được bỏ trống")]
    [StringLength(2000, ErrorMessage = "Mô tả tối đa 2000 ký tự")]
    [Display(Name = "Mô tả sản phẩm")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Giá sản phẩm không được bỏ trống")]
    [Range(0.01, 999999999, ErrorMessage = "Giá phải lớn hơn 0")]
    [Display(Name = "Giá bán (VNĐ)")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Số lượng không được bỏ trống")]
    [Range(1, 9999, ErrorMessage = "Số lượng phải từ 1 đến 9999")]
    [Display(Name = "Số lượng")]
    public int Quantity { get; set; } = 1;

    [Display(Name = "Sản phẩm mới")]
    public bool IsNew { get; set; } = false;

    [Required(ErrorMessage = "Vui lòng chọn danh mục cha")]
    [Display(Name = "Danh mục cha")]
    public int? ParentCategoryId { get; set; }

    [Display(Name = "Danh mục sản phẩm")]
    public int? CategoryId { get; set; }

    [StringLength(255, ErrorMessage = "Địa điểm tối đa 255 ký tự")]
    [Display(Name = "Địa điểm bán")]
    public string? Location { get; set; }
    [Display(Name = "Thông số kỹ thuật")]
    public string? Specifications { get; set; }

    // Ảnh sản phẩm sẽ được lấy từ Request.Form.Files trong Controller, không cần trong ViewModel
    // Chỉ dùng để validation message
    [Display(Name = "Ảnh sản phẩm (tối thiểu 3 ảnh)")]
    public string? ProductImages { get; set; } // Placeholder để validation

    [Required(ErrorMessage = "Vui lòng chọn ảnh chính")]
    [Display(Name = "Ảnh chính (chọn số thứ tự)")]
    public int MainImageIndex { get; set; } = 0; // Index của ảnh chính trong danh sách (0-based)
}
