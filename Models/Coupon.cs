using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopDoCu.Models
{
    [Table("Coupons")]
    public partial class Coupon
    {
        // Khóa chính mã coupon
        [Key]
        [Required(ErrorMessage = "Không được bỏ trống")]
        public int CouponId { get; set; }

        // Mã code (hiển thị/tra cứu) - không bắt buộc unique nếu bạn muốn nhiều coupon giống code khác context
        [StringLength(50)]
        [Required(ErrorMessage = "Không được bỏ trống")]
        public string? Code { get; set; }

        // Loại giảm giá ("Percent" hoặc "Fixed")
        [Required(ErrorMessage = "Không được bỏ trống")]
        [StringLength(20)]
        public string? DiscountType { get; set; }

        // Giá trị giảm (số tiền hoặc phần trăm)
        [Required(ErrorMessage = "Không được bỏ trống")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        // Số lượt còn lại có thể dùng
        [Required(ErrorMessage = "Không được bỏ trống")]
        public int Quantity { get; set; }

        // Có đang hoạt động không
        public bool IsActive { get; set; } = true;

        // Ngày bắt đầu hiệu lực
        [Required(ErrorMessage = "Không được bỏ trống")]
        public DateTime StartDate { get; set; }

        // Ngày kết thúc hiệu lực
        [Required(ErrorMessage = "Không được bỏ trống")]
        public DateTime EndDate { get; set; }

        // Điều kiện đơn hàng tối thiểu để áp dụng coupon
        [Required(ErrorMessage = "Không được bỏ trống")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MinOrderAmount { get; set; }

        // Nếu SellerId == null => đây là platform coupon (Admin chịu chi)
        public int? SellerId { get; set; }

        // Navigation: Seller (nếu coupon thuộc shop)
        public virtual User? Seller { get; set; }

        // Navigation: Lịch sử sử dụng coupon (không xóa coupon khi dùng, ghi log ở đây)
        public virtual ICollection<CouponUsage> CouponUsages { get; set; } = new List<CouponUsage>();
    }
}