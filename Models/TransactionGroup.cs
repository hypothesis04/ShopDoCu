using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopDoCu.Models
{
    [Table("TransactionGroups")]
    public partial class TransactionGroup
    {
        // Khóa chính: TransactionGroupId dùng để gom nhiều Order từ 1 lần Checkout
        [Key]
        public Guid TransactionGroupId { get; set; } = Guid.NewGuid();

        // FK: Người dùng thực hiện checkout (buyer)
        public int? UserId { get; set; }

        // Thời điểm tạo TransactionGroup (một checkout)
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Tổng tiền của cả Transaction (cộng tất cả Order totals + shipping - platform coupon)
        public decimal TotalAmount { get; set; }

        // Hình thức thanh toán (VD: COD, Online)
        public string? PaymentMethod { get; set; }

        // Trạng thái thanh toán (VD: Unpaid, Paid, Refunded)
        public string? PaymentStatus { get; set; }

        // Thời gian thanh toán (nếu có)
        public DateTime? PaymentDate { get; set; }

        // Navigation: Người dùng (buyer)
        public virtual User? User { get; set; }

        // Navigation: Danh sách Orders con (mỗi Order tương ứng 1 seller)
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}