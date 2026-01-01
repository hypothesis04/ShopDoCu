using System;
using Microsoft.EntityFrameworkCore;

namespace ShopDoCu.Models;

public partial class ChoBanDoCuContext : DbContext
{
    // Default ctor
    public ChoBanDoCuContext()
    {
    }

    // Ctor dùng DI
    public ChoBanDoCuContext(DbContextOptions<ChoBanDoCuContext> options)
        : base(options)
    {
    }

    // DbSets cho các bảng hiện có
    public virtual DbSet<Cart> Carts { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<Message> Messages { get; set; }
    public virtual DbSet<Order> Orders { get; set; }
    public virtual DbSet<OrderDetail> OrderDetails { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<ProductImage> ProductImages { get; set; }
    public virtual DbSet<ProductTag> ProductTags { get; set; }
    public virtual DbSet<Review> Reviews { get; set; }
    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserCoupon> UserCoupons { get; set; }
    public virtual DbSet<Coupon> Coupons { get; set; }
    public virtual DbSet<CouponUsage> CouponUsages { get; set; }

    // TransactionGroup (mỗi checkout tạo 1 TransactionGroup gom nhiều Orders)
    public virtual DbSet<TransactionGroup> TransactionGroups { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Server=ADMIN-PC\\SQLEXPRESS2;Database=ChoBanDoCu;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Cart config
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("PK__Cart__51BCD7B75808DB37");

            entity.ToTable("Cart");

            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Quantity).HasDefaultValue(1);

            entity.HasOne(d => d.Product).WithMany(p => p.Carts)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_Cart_Product");

            entity.HasOne(d => d.User).WithMany(p => p.Carts)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Cart_User");
        });

        // Category config
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A0BE671214B");

            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(255);

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("FK_Category_Parent");
        });

        // Message config
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK__Messages__C87C0C9CE61ABB68");

            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Product).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_Message_Product");

            entity.HasOne(d => d.Receiver).WithMany(p => p.MessageReceivers)
                .HasForeignKey(d => d.ReceiverId)
                .HasConstraintName("FK_Message_Receiver");

            entity.HasOne(d => d.Sender).WithMany(p => p.MessageSenders)
                .HasForeignKey(d => d.SenderId)
                .HasConstraintName("FK_Message_Sender");
        });

        // Order config (per-seller order)
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BCF020F596B");

            entity.ToTable("Orders");

            entity.Property(e => e.OrderDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.ShippingFee).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentDate).HasColumnType("datetime");
            entity.Property(e => e.PaymentMethod).HasMaxLength(50);
            entity.Property(e => e.PaymentStatus).HasMaxLength(50);
            entity.Property(e => e.ShippingAddress).HasMaxLength(255);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.CouponCode).HasMaxLength(50);
            entity.Property(e => e.ReceiverName).HasMaxLength(100);
            entity.Property(e => e.ReceiverPhone).HasMaxLength(20);

            // Map TransactionGroupId as uniqueidentifier column and FK -> TransactionGroups
            entity.Property(e => e.TransactionGroupId).HasColumnType("uniqueidentifier");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Orders_User");

            entity.HasOne(d => d.Seller).WithMany()
                .HasForeignKey(d => d.SellerId)
                .HasConstraintName("FK_Orders_Seller");

            entity.HasOne(d => d.TransactionGroup).WithMany(t => t.Orders)
                .HasForeignKey(d => d.TransactionGroupId)
                .HasConstraintName("FK_Orders_TransactionGroup");
        });

        // OrderDetail config
        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId).HasName("PK__OrderDet__D3B9D36CAB6E8A43");

            entity.ToTable("OrderDetails");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_OrderDetail_Order");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_OrderDetail_Product");
        });

        // Product config
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6CD8BE781BF");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsNew).HasDefaultValue(false);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ProductName).HasMaxLength(150);
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Active");
            entity.Property(e => e.Views).HasDefaultValue(0);

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK_Products_Category");

            entity.HasOne(d => d.Seller).WithMany(p => p.Products)
                .HasForeignKey(d => d.SellerId)
                .HasConstraintName("FK_Products_Seller");
        });

        // ProductImage config
        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.ImageId).HasName("PK__ProductI__7516F70CF890C3A6");

            entity.Property(e => e.ImageUrl).HasMaxLength(255);
            entity.Property(e => e.IsMain).HasDefaultValue(false);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_ProductImages_Product");
        });

        // ProductTag config
        modelBuilder.Entity<ProductTag>(entity =>
        {
            entity.HasKey(e => e.TagId).HasName("PK__ProductT__657CF9AC381C6CA7");

            entity.Property(e => e.TagName).HasMaxLength(50);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductTags)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_ProductTags_Product");
        });

        // Review config
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("PK__Reviews__74BC79CEAA161093");

            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Product).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_Reviews_Product");

            entity.HasOne(d => d.User).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Reviews_User");
        });

        // User config
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C525D7EA4");

            entity.HasIndex(e => e.UserName, "UQ__Users__C9F284561243F835").IsUnique();

            entity.Property(e => e.AvatarUrl).HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.IsLocked).HasDefaultValue(false);
            entity.Property(e => e.LockReason).HasMaxLength(255);
            entity.Property(e => e.LockedAt).HasColumnType("datetime");
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(15);
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValue("User");
            entity.Property(e => e.UserName).HasMaxLength(50);
        });

        // Coupon config
        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasKey(e => e.CouponId);

            entity.ToTable("Coupons");

            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.DiscountType).HasMaxLength(20);
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18 ,2)");
            entity.Property(e => e.MinOrderAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            // Nếu coupon thuộc Seller thì liên kết
            entity.HasOne(d => d.Seller).WithMany()
                .HasForeignKey(d => d.SellerId)
                .HasConstraintName("FK_Coupons_Seller");
        });

        // CouponUsage config (ghi lại mỗi lần dùng)
        modelBuilder.Entity<CouponUsage>(entity =>
        {
            entity.HasKey(e => e.CouponUsageId).HasName("PK_CouponUsage");

            entity.ToTable("CouponUsages");

            entity.Property(e => e.AppliedAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.UsedAt).HasColumnType("datetime");

            // Liên kết đến Coupon
            entity.HasOne(d => d.Coupon).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.CouponId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_CouponUsage_Coupon");

            // Liên kết đến User
            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_CouponUsage_User");

            // Liên kết đến Order (nếu coupon áp dụng cho order cụ thể)
            entity.HasOne(d => d.Order).WithMany()
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_CouponUsage_Order");

            // Liên kết đến TransactionGroup (nếu coupon áp dụng cho transaction / platform)
            entity.HasOne(d => d.TransactionGroup).WithMany()
                .HasForeignKey(d => d.TransactionGroupId)
                .HasConstraintName("FK_CouponUsage_TransactionGroup");
        });

        // TransactionGroup config
        modelBuilder.Entity<TransactionGroup>(entity =>
        {
            entity.HasKey(e => e.TransactionGroupId).HasName("PK_TransactionGroup");

            entity.ToTable("TransactionGroups");

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PaymentDate).HasColumnType("datetime");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_TransactionGroup_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
