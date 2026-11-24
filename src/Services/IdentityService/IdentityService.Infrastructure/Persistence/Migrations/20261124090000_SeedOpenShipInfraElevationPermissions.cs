using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data-only migration: production runs migrations through the persistence job but never the
    /// startup seeder, so catalog permissions added after the first deployment must arrive here.
    /// openship.node.root and openship.cluster.admin are break-glass-only elevations for OpenShip
    /// infrastructure access. While an approved grant is active, the token issued to the OpenShip
    /// access clients carries the matching openship-breakglass-* group.
    /// </summary>
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20261124090000_SeedOpenShipInfraElevationPermissions")]
    public partial class SeedOpenShipInfraElevationPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO iam_permissions (code, name, "group", description, is_system, is_deleted, created_at)
                VALUES
                    ('openship.node.root',
                     'Root trên node OpenShip',
                     'OpenShip',
                     'Quyền SSH root tạm thời trên node K3s OpenShip, chỉ cấp qua break-glass',
                     TRUE, FALSE, now()),
                    ('openship.cluster.admin',
                     'Cluster-admin K3s OpenShip',
                     'OpenShip',
                     'Quyền cluster-admin Kubernetes tạm thời trên cluster OpenShip, chỉ cấp qua break-glass',
                     TRUE, FALSE, now())
                ON CONFLICT (code) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM iam_permissions WHERE code IN ('openship.node.root', 'openship.cluster.admin') AND is_system;");
        }
    }
}
