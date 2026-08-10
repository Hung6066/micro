using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileDeliveryAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "push_delivery_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    outbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    error_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_push_delivery_attempts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_push_delivery_attempts_created_at_platform_status",
                table: "push_delivery_attempts",
                columns: new[] { "created_at", "platform", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_push_delivery_attempts_device_id",
                table: "push_delivery_attempts",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_push_delivery_attempts_outbox_id",
                table: "push_delivery_attempts",
                column: "outbox_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "push_delivery_attempts");
        }
    }
}
