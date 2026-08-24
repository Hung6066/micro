using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommerceNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "commerce_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commerce_notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_commerce_notifications_TenantKey_UserId_CreatedAt",
                table: "commerce_notifications",
                columns: new[] { "TenantKey", "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commerce_notifications");
        }
    }
}
