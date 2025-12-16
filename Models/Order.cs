using System;
using System.Collections.Generic;

namespace ShopDoCu.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public int? UserId { get; set; }

    public DateTime? OrderDate { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? Status { get; set; }

    public string? ShippingAddress { get; set; }

    public string? PaymentMethod { get; set; }

    public string? PaymentStatus { get; set; }

    public DateTime? PaymentDate { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual User? User { get; set; }

    public string? CouponCode { get; set; } // Cho phép null

    public decimal DiscountAmount { get; set; } = 0; // Mặc định là 0
    
    public string? ReceiverName { get; set; } // Thêm dòng này

    public string? ReceiverPhone { get; set; } // Thêm dòng này
}
