using System;
using System.Collections.Generic;

namespace ShopDoCu.Models;

public partial class User
{
    public int UserId { get; set; }

    public string UserName { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? FullName { get; set; }

    public string? Email { get; set; }

    public string Phone { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Role { get; set; }

    public string? Address { get; set; }

    // Thông tin thanh toán (số tài khoản ngân hàng, tên ngân hàng, v.v.)
    public string? PaymentInfo { get; set; }

    // Tên cửa hàng (dành cho Seller)
    public string? StoreName { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsLocked { get; set; }

    public DateTime? LockedAt { get; set; }

    public string? LockReason { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<Message> MessageReceivers { get; set; } = new List<Message>();

    public virtual ICollection<Message> MessageSenders { get; set; } = new List<Message>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
