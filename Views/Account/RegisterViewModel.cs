
using System.ComponentModel.DataAnnotations;

namespace ShopDoCu.Views.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Tên đăng nhập không được bỏ trống")]
    [StringLength(50, ErrorMessage = "Tên đăng nhập tối đa 50 ký tự")]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên không được bỏ trống")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được bỏ trống")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [StringLength(15, ErrorMessage = "Số điện thoại tối đa 15 ký tự")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự")]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Mật khẩu không được bỏ trống")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập lại mật khẩu")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Mật khẩu nhập lại không khớp")]
    [Display(Name = "Nhập lại mật khẩu")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

