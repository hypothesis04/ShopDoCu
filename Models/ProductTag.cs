using System;
using System.Collections.Generic;

namespace ShopDoCu.Models;

public partial class ProductTag
{
    public int TagId { get; set; }

    public string? TagName { get; set; }

    public int? ProductId { get; set; }

    public virtual Product? Product { get; set; }
}
