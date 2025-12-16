using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopDoCu.Models
{
    [Table("Coupons")]
    public class Coupon
    {
        [Key]
        public int CouponId { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; }

        public decimal DiscountValue { get; set; } // Số tiền hoặc Số %

        [Required]
        [StringLength(20)]
        public string DiscountType { get; set; } // "Percent" hoặc "Fixed"

        public decimal MinOrderAmount { get; set; } // Đơn tối thiểu

        public int Quantity { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; }
    }
}