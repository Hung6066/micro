using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommerceCarts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "commerce_carts",
                columns: table => new
                {
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commerce_carts", x => new { x.TenantKey, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "commerce_cart_lines",
                columns: table => new
                {
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commerce_cart_lines", x => new { x.TenantKey, x.UserId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_commerce_cart_lines_commerce_carts_TenantKey_UserId",
                        columns: x => new { x.TenantKey, x.UserId },
                        principalTable: "commerce_carts",
                        principalColumns: new[] { "TenantKey", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_commerce_cart_lines_TenantKey_UserId",
                table: "commerce_cart_lines",
                columns: new[] { "TenantKey", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commerce_cart_lines");

            migrationBuilder.DropTable(
                name: "commerce_carts");
        }
    }
}
