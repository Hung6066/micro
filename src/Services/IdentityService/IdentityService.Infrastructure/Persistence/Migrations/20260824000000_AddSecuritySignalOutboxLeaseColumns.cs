using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations;

public partial class AddSecuritySignalOutboxLeaseColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE security_signal_outbox ADD COLUMN IF NOT EXISTS lease_id uuid;");
        migrationBuilder.Sql("ALTER TABLE security_signal_outbox ADD COLUMN IF NOT EXISTS lease_until timestamp with time zone;");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_security_signal_outbox_dispatched_at_lease_until_available_at ON security_signal_outbox (dispatched_at, lease_until, available_at);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_security_signal_outbox_dispatched_at_lease_until_available_at;");
        migrationBuilder.Sql("ALTER TABLE security_signal_outbox DROP COLUMN IF EXISTS lease_id;");
        migrationBuilder.Sql("ALTER TABLE security_signal_outbox DROP COLUMN IF EXISTS lease_until;");
    }
}
