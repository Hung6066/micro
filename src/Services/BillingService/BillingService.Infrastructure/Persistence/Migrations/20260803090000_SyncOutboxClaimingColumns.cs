using Microsoft.EntityFrameworkCore.Migrations;

namespace His.Hope.BillingService.Infrastructure.Persistence.Migrations;

[Migration("20260803090000_SyncOutboxClaimingColumns")]
public partial class SyncOutboxClaimingColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'billing' AND table_name = 'OutboxMessages')
              THEN RETURN; END IF;
              IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'billing' AND table_name = 'OutboxMessages' AND column_name = 'claimed_by')
                 AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'billing' AND table_name = 'OutboxMessages' AND column_name = 'ClaimedBy')
              THEN ALTER TABLE billing."OutboxMessages" RENAME COLUMN claimed_by TO "ClaimedBy"; END IF;
              IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'billing' AND table_name = 'OutboxMessages' AND column_name = 'next_attempt_at')
                 AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'billing' AND table_name = 'OutboxMessages' AND column_name = 'NextAttemptAt')
              THEN ALTER TABLE billing."OutboxMessages" RENAME COLUMN next_attempt_at TO "NextAttemptAt"; END IF;
              IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'billing' AND table_name = 'OutboxMessages' AND column_name = 'dead_lettered_on')
                 AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'billing' AND table_name = 'OutboxMessages' AND column_name = 'DeadLetteredOn')
              THEN ALTER TABLE billing."OutboxMessages" RENAME COLUMN dead_lettered_on TO "DeadLetteredOn"; END IF;
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'billing' AND table_name = 'OutboxMessages' AND column_name = 'ClaimedBy')
              THEN ALTER TABLE billing."OutboxMessages" ADD COLUMN "ClaimedBy" varchar(100); END IF;
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'billing' AND table_name = 'OutboxMessages' AND column_name = 'NextAttemptAt')
              THEN ALTER TABLE billing."OutboxMessages" ADD COLUMN "NextAttemptAt" timestamptz; END IF;
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'billing' AND table_name = 'OutboxMessages' AND column_name = 'DeadLetteredOn')
              THEN ALTER TABLE billing."OutboxMessages" ADD COLUMN "DeadLetteredOn" timestamptz; END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
