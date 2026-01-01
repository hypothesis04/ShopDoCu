using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopDoCu.Models
{
    [Table("OrderDetails")]
    public partial class OrderDetail
    {
        // Khóa chính của OrderDetail
        [Key]
        public int OrderDetailId { get; set; }

        // FK: Order chứa detail này
        public int? OrderId { get; set; }

        // FK: Product được mua
        public int? ProductId { get; set; }

        // Số lượng mua
        public int? Quantity { get; set; }

        // Giá bán tại thời điểm đặt hàng (snapshot)
        [Column(TypeName = "decimal(18,2)")]
        public decimal? UnitPrice { get; set; }

        // Snapshot SellerId của sản phẩm tại thời điểm đặt hàng (giúp truy vấn nhanh)
        public int? SellerId { get; set; }

        // Navigation: Order
        public virtual Order? Order { get; set; }

        // Navigation: Product
        public virtual Product? Product { get; set; }
    }
}
