using System.ComponentModel.DataAnnotations;

namespace ShopDoCu.Views.Account;

public class LoginViewModel
{
    [Required(ErrorMessage = "Tên đăng nhập không được bỏ trống")]
    [StringLength(50, ErrorMessage = "Tên đăng nhập tối đa 50 ký tự")]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được bỏ trống")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

