using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopDoCu.Models
{
    [Table("Orders")]
    public partial class Order
    {
        // Khóa chính của Order (một package dành cho 1 Seller)
        [Key]
        public int OrderId { get; set; }

        // FK: Người mua (buyer) tạo đơn này
        public int? UserId { get; set; }

        // Thời điểm tạo đơn (của phần seller này)
        public DateTime? OrderDate { get; set; }

        // Tổng tiền hàng (chưa bao gồm phí vận chuyển, chưa trừ coupon) cho seller này
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        // Phí vận chuyển dành cho seller này
        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingFee { get; set; }

        // Tổng tiền phải trả cho order này (Subtotal + ShippingFee - DiscountAmount)
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAmount { get; set; }

        // Trạng thái đơn của phần seller này (Pending, Shipping, Completed, Cancelled...)
        [MaxLength(50)]
        public string? Status { get; set; }

        // Địa chỉ giao hàng snapshot
        [MaxLength(255)]
        public string? ShippingAddress { get; set; }

        // Hình thức thanh toán cho order này
        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        // Trạng thái thanh toán (Paid/Unpaid/Refunded)
        [MaxLength(50)]
        public string? PaymentStatus { get; set; }

        // Thời điểm thanh toán (nếu có)
        public DateTime? PaymentDate { get; set; }

        // Mã coupon áp dụng trực tiếp cho order này (seller coupon)
        [MaxLength(50)]
        public string? CouponCode { get; set; }

        // Số tiền giảm do coupon seller áp dụng lên order này
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0m;

        // Tên người nhận snapshot
        [MaxLength(100)]
        public string? ReceiverName { get; set; }

        // SĐT người nhận snapshot
        [MaxLength(20)]
        public string? ReceiverPhone { get; set; }

        // FK: TransactionGroupId để liên kết nhiều Orders của cùng 1 lần checkout
        public Guid? TransactionGroupId { get; set; }

        // Navigation: TransactionGroup chứa thông tin tổng giao dịch
        public virtual TransactionGroup? TransactionGroup { get; set; }

        // FK: Seller (chủ shop) chịu trách nhiệm phần order này
        public int? SellerId { get; set; }

        // Navigation: Seller (User với Role = Seller)
        public virtual User? Seller { get; set; }

        // Navigation: Chi tiết đơn hàng
        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

        // Navigation: Buyer (User)
        public virtual User? User { get; set; }
    }
}
