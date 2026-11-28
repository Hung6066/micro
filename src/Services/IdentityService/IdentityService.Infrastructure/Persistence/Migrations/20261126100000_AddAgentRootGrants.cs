using System;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Four-eyes, time-limited root SSH grants for the infrastructure automation agent. The
    /// certificate column holds the Vault-signed OpenSSH certificate of an issued grant.
    /// </summary>
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20261126100000_AddAgentRootGrants")]
    public partial class AddAgentRootGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_root_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_by_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valid_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    certificate_serial = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    certificate = table.Column<string>(type: "text", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table => table.PrimaryKey("pk_agent_root_grants", x => x.id));

            migrationBuilder.CreateIndex(
                name: "ix_agent_root_grants_agent_id_status",
                table: "agent_root_grants",
                columns: new[] { "agent_id", "status" });

            // Production never runs the startup seeder, so the workload scope used by the
            // certificate fetcher's client_credentials client must arrive here.
            migrationBuilder.Sql("""
                INSERT INTO openiddict_scopes (id, concurrency_token, name, display_name, resources, is_deleted, created_at)
                SELECT gen_random_uuid()::text, gen_random_uuid()::text, 'openship.agent.root.certificate',
                       'His.Hope - OPENSHIP.AGENT.ROOT.CERTIFICATE', '["his-hope-services"]', FALSE, now()
                WHERE NOT EXISTS (SELECT 1 FROM openiddict_scopes WHERE name = 'openship.agent.root.certificate');
                """);

            // Admin-app navigation label and page title.
            migrationBuilder.Sql("""
                INSERT INTO localization_resources (scope_id, key, description, is_deleted, created_at)
                SELECT 'global', 'admin.agentRootAccess', 'Admin navigation: agent root SSH grants', FALSE, now()
                WHERE NOT EXISTS (SELECT 1 FROM localization_resources WHERE scope_id = 'global' AND key = 'admin.agentRootAccess');
                INSERT INTO localization_translations (scope_id, resource_key, locale, value, is_deleted, created_at)
                SELECT 'global', 'admin.agentRootAccess', locale, value, FALSE, now()
                FROM (VALUES ('en-US', 'Agent root access'), ('vi-VN', 'Quyền root cho agent')) AS seed(locale, value)
                WHERE NOT EXISTS (
                    SELECT 1 FROM localization_translations existing
                    WHERE existing.scope_id = 'global' AND existing.resource_key = 'admin.agentRootAccess' AND existing.locale = seed.locale);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM localization_translations WHERE scope_id = 'global' AND resource_key = 'admin.agentRootAccess';
                DELETE FROM localization_resources WHERE scope_id = 'global' AND key = 'admin.agentRootAccess';
                DELETE FROM openiddict_scopes WHERE name = 'openship.agent.root.certificate';
                """);
            migrationBuilder.DropTable(name: "agent_root_grants");
        }
    }
}
