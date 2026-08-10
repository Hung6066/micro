using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations;

public partial class AddInAppNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "in_app_notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                data_json = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_in_app_notifications", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_in_app_notifications_user_id_read_at_created_at",
            table: "in_app_notifications",
            columns: new[] { "user_id", "read_at", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "in_app_notifications");
    }
}
