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
        public int CouponId { get; set; }

        // Mã code (hiển thị/tra cứu) - không bắt buộc unique nếu bạn muốn nhiều coupon giống code khác context
        [StringLength(50)]
        public string? Code { get; set; }

        // Loại giảm giá ("Percent" hoặc "Fixed")
        [StringLength(20)]
        public string? DiscountType { get; set; }

        // Giá trị giảm (số tiền hoặc phần trăm)
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        // Số lượt còn lại có thể dùng
        public int Quantity { get; set; }

        // Có đang hoạt động không
        public bool IsActive { get; set; } = true;

        // Ngày bắt đầu hiệu lực
        public DateTime StartDate { get; set; }

        // Ngày kết thúc hiệu lực
        public DateTime EndDate { get; set; }

        // Điều kiện đơn hàng tối thiểu để áp dụng coupon
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