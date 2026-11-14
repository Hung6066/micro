using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations;

/// <summary>Adds the lease fields mapped by SecuritySignalOutbox to databases created from the original outbox migration.</summary>
[Migration("20260824070000_AddSecuritySignalOutboxLeaseColumns")]
public partial class AddSecuritySignalOutboxLeaseColumnsFollowUp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This follow-up is retained for databases that were stamped with the
        // original outbox migration before lease columns were generated. The
        // canonical 20260824000000 migration may already have applied them;
        // use catalog checks so replaying the migration chain is safe.
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF to_regclass('security_signal_outbox') IS NULL THEN
                    RETURN;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'security_signal_outbox' AND column_name = 'lease_id'
                ) THEN
                    ALTER TABLE security_signal_outbox ADD COLUMN lease_id uuid;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'security_signal_outbox' AND column_name = 'lease_until'
                ) THEN
                    ALTER TABLE security_signal_outbox ADD COLUMN lease_until timestamp with time zone;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM pg_class WHERE relname = 'ix_security_signal_outbox_dispatched_at_lease_until_available_at'
                ) THEN
                    CREATE INDEX ix_security_signal_outbox_dispatched_at_lease_until_available_at
                    ON security_signal_outbox (dispatched_at, lease_until, available_at);
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS ix_security_signal_outbox_dispatched_at_lease_until_available_at;
            ALTER TABLE IF EXISTS security_signal_outbox DROP COLUMN IF EXISTS lease_id;
            ALTER TABLE IF EXISTS security_signal_outbox DROP COLUMN IF EXISTS lease_until;
            """);
    }
}
