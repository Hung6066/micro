using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePlatformPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mobile_device_registrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    token_ciphertext = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    registered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mobile_device_registrations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mobile_telemetry_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    stack = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    route = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    app_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    duration_ms = table.Column<double>(type: "double precision", nullable: true),
                    metadata_json = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mobile_telemetry_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mobile_device_registrations_user_id_platform_token_hash",
                table: "mobile_device_registrations",
                columns: new[] { "user_id", "platform", "token_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mobile_device_registrations_user_id_revoked_at",
                table: "mobile_device_registrations",
                columns: new[] { "user_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mobile_telemetry_events_event_type_created_at",
                table: "mobile_telemetry_events",
                columns: new[] { "event_type", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mobile_device_registrations");

            migrationBuilder.DropTable(
                name: "mobile_telemetry_events");
        }
    }
}
