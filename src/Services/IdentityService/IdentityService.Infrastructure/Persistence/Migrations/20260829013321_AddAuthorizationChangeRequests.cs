using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations;

public partial class AddAuthorizationChangeRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "authorization_change_requests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                resource_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                requested_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                payload_json = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                approved_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_authorization_change_requests", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ix_authorization_change_requests_resource_type_resource_id_action_status",
            table: "authorization_change_requests",
            columns: new[] { "resource_type", "resource_id", "action", "status" });
        migrationBuilder.CreateIndex(
            name: "ix_authorization_change_requests_status_expires_at",
            table: "authorization_change_requests",
            columns: new[] { "status", "expires_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "authorization_change_requests");
}
