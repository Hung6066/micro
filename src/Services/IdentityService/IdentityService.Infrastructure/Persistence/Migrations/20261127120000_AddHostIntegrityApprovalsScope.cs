using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data only (no model change): the workload scope the OpenShip host integrity check uses to
    /// pull drift approvals made in admin-app Security. Production never runs the startup seeder.
    /// </summary>
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20261127120000_AddHostIntegrityApprovalsScope")]
    public partial class AddHostIntegrityApprovalsScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO openiddict_scopes (id, concurrency_token, name, display_name, resources, is_deleted, created_at)
                SELECT gen_random_uuid()::text, gen_random_uuid()::text, 'openship.host.integrity.approvals',
                       'His.Hope - OPENSHIP.HOST.INTEGRITY.APPROVALS', '["his-hope-services"]', FALSE, now()
                WHERE NOT EXISTS (SELECT 1 FROM openiddict_scopes WHERE name = 'openship.host.integrity.approvals');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM openiddict_scopes WHERE name = 'openship.host.integrity.approvals';");
        }
    }
}
