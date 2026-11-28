using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data-only migration: production runs migrations through the persistence job but never the
    /// startup seeder, so catalog permissions added after the first deployment must arrive here.
    /// openship.console.write is the break-glass-only write grant for the OpenShip tailnet consoles.
    /// </summary>
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20261122150000_SeedOpenShipConsoleWritePermission")]
    public partial class SeedOpenShipConsoleWritePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO iam_permissions (code, name, "group", description, is_system, is_deleted, created_at)
                VALUES ('openship.console.write',
                        'Ghi trên console OpenShip',
                        'OpenShip',
                        'Quyền ghi tạm thời trên console tailnet OpenShip, chỉ cấp qua break-glass',
                        TRUE, FALSE, now())
                ON CONFLICT (code) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM iam_permissions WHERE code = 'openship.console.write' AND is_system;");
        }
    }
}
