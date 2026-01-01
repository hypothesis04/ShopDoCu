using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopDoCu.Models
{
    [Table("CouponUsages")]
    public partial class CouponUsage
    {
        // Khóa chính cho từng lần sử dụng coupon
        [Key]
        public int CouponUsageId { get; set; }

        // FK: Coupon được sử dụng
        public int CouponId { get; set; }

        // FK: User (ai dùng coupon)
        public int? UserId { get; set; }

        // FK: Order (nếu coupon áp dụng cho một seller order)
        public int? OrderId { get; set; }

        // FK: TransactionGroup (nếu coupon áp dụng cho toàn transaction / platform coupon)
        public Guid? TransactionGroupId { get; set; }

        // Số tiền thực tế đã giảm (ghi giá trị thực tế để audit)
        [Column(TypeName = "decimal(18,2)")]
        public decimal AppliedAmount { get; set; }

        // Thời điểm sử dụng
        public DateTime UsedAt { get; set; } = DateTime.Now;

        // Ghi chú thêm (vd: "seller coupon", "platform coupon", hoặc mô tả)
        [MaxLength(255)]
        public string? Note { get; set; }

        // Navigation: Coupon
        public virtual Coupon Coupon { get; set; } = null!;

        // Navigation: Order (nếu có)
        public virtual Order? Order { get; set; }

        // Navigation: TransactionGroup (nếu có)
        public virtual TransactionGroup? TransactionGroup { get; set; }

        // Navigation: User
        public virtual User? User { get; set; }
    }
}