using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopDoCu.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCouponTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserCoupons",
                columns: table => new
                {
                    UserCouponId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCoupons", x => x.UserCouponId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCoupons");
        }
    }
}
