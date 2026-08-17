using Microsoft.EntityFrameworkCore.Migrations;

namespace His.Hope.LabService.Infrastructure.Persistence.Migrations;

[Migration("20260803090000_SyncOutboxClaimingColumns")]
public partial class SyncOutboxClaimingColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "OutboxMessages"
              ADD COLUMN IF NOT EXISTS claimed_by varchar(100),
              ADD COLUMN IF NOT EXISTS next_attempt_at timestamptz,
              ADD COLUMN IF NOT EXISTS dead_lettered_on timestamptz;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
