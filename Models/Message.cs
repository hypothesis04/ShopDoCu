using System;
using System.Collections.Generic;

namespace ShopDoCu.Models;

public partial class Message
{
    public int MessageId { get; set; }

    public int? SenderId { get; set; }

    public int? ReceiverId { get; set; }

    public int? ProductId { get; set; }

    public string? Content { get; set; }

    public DateTime? SentAt { get; set; }

    public bool? IsRead { get; set; }

    public virtual Product? Product { get; set; }

    public virtual User? Receiver { get; set; }

    public virtual User? Sender { get; set; }
}
