using System;
using System.Collections.Generic;

namespace ShopDoCu.Models;

public partial class Product
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int? Quantity { get; set; }

    public bool? IsNew { get; set; }

    public int? CategoryId { get; set; }

    public int? SellerId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? Views { get; set; }

    public string? Status { get; set; }

    // Địa điểm bán cụ thể (có thể khác với địa chỉ của Seller, ví dụ: "Quận 1, TP.HCM")
    public string? Location { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual Category? Category { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual User? Seller { get; set; }
}
