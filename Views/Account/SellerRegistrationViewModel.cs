using System.ComponentModel.DataAnnotations;

namespace ShopDoCu.Views.Account;

// ViewModel cho form đăng ký thành người bán - chứa đầy đủ thông tin User + thông tin thanh toán
public class SellerRegistrationViewModel
{
    [Required(ErrorMessage = "Họ tên không được bỏ trống")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được bỏ trống")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(100)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được bỏ trống")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(15, ErrorMessage = "Số điện thoại tối đa 15 ký tự")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Địa chỉ không được bỏ trống")]
    [StringLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự")]
    [Display(Name = "Địa chỉ")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên cửa hàng không được bỏ trống")]
    [StringLength(255, ErrorMessage = "Tên cửa hàng tối đa 255 ký tự")]
    [Display(Name = "Tên cửa hàng")]
    public string StoreName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Thông tin thanh toán không được bỏ trống")]
    [StringLength(500, ErrorMessage = "Thông tin thanh toán tối đa 500 ký tự")]
    [Display(Name = "Thông tin thanh toán")]
    public string PaymentInfo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bạn phải đồng ý với điều khoản")]
    [Display(Name = "Tôi đồng ý với các điều khoản")]
    public bool AgreeToTerms { get; set; }
}
