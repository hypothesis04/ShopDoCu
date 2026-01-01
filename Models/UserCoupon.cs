using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopDoCu.Models
{
    // Bảng này lưu các mã giảm giá mà người dùng ĐANG CÓ
    [Table("UserCoupons")]
    public class UserCoupon
    {
        [Key]
        public int UserCouponId { get; set; }
        public int UserId { get; set; }
        public string Code { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }
        
        public bool IsActive { get; set; } = true;
    }
}