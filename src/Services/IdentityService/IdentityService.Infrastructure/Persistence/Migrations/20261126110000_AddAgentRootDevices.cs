using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRootDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_root_certificates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    certificate = table.Column<string>(type: "text", nullable: false),
                    serial = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    valid_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_root_certificates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_root_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    public_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    client_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    added_by = table.Column<Guid>(type: "uuid", nullable: false),
                    added_by_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    disabled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    disabled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_root_devices", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_root_certificates_device_id_valid_before",
                table: "agent_root_certificates",
                columns: new[] { "device_id", "valid_before" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_root_certificates_grant_id",
                table: "agent_root_certificates",
                column: "grant_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_root_devices_client_id",
                table: "agent_root_devices",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_root_devices_fingerprint",
                table: "agent_root_devices",
                column: "fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_root_devices_name",
                table: "agent_root_devices",
                column: "name",
                unique: true);

            // The K3s manager (vmi3129107) becomes the first agent device: the key that was
            // configured as InfraAccess:AgentRoot:PublicKey, fetched with the existing client.
            migrationBuilder.Sql("""
                INSERT INTO agent_root_devices (id, name, public_key, fingerprint, client_id, is_active, added_by, added_by_email, added_at)
                SELECT '7d0c5c2e-5b7a-4f37-9d3c-0a1f6e2b9c41', 'k3s-manager-vmi3129107',
                       'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIPnImciBKtlzOkC88dd+OXfF6Qn8WwhNchbO4m8i4ipK openship-agent-root@vmi3129107', 'SHA256:RUmGPD+IkmTBowPW8HKHissVnfpqEt1KiYpKsWjWZsU', 'openship-agent-root-fetcher', TRUE,
                       '00000000-0000-0000-0000-000000000000', 'migration', now()
                WHERE NOT EXISTS (SELECT 1 FROM agent_root_devices WHERE fingerprint = 'SHA256:RUmGPD+IkmTBowPW8HKHissVnfpqEt1KiYpKsWjWZsU');
                """);

            // Grants issued before devices existed signed that key; keep their certificates so
            // the manager does not lose an active grant during the upgrade.
            migrationBuilder.Sql("""
                INSERT INTO agent_root_certificates (id, grant_id, device_id, device_name, certificate, serial, valid_before, issued_at)
                SELECT gen_random_uuid(), g.id, '7d0c5c2e-5b7a-4f37-9d3c-0a1f6e2b9c41', 'k3s-manager-vmi3129107',
                       g.certificate, COALESCE(g.certificate_serial, ''), g.valid_before, COALESCE(g.approved_at, now())
                FROM agent_root_grants g
                WHERE g.certificate IS NOT NULL AND g.valid_before IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "certificate",
                table: "agent_root_grants");

            migrationBuilder.DropColumn(
                name: "certificate_serial",
                table: "agent_root_grants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_root_certificates");

            migrationBuilder.DropTable(
                name: "agent_root_devices");

            migrationBuilder.AddColumn<string>(
                name: "certificate",
                table: "agent_root_grants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_serial",
                table: "agent_root_grants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
