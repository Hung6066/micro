using Microsoft.EntityFrameworkCore.Migrations;

namespace His.Hope.PharmacyService.Infrastructure.Persistence.Migrations;

[Migration("20260803090000_SyncOutboxClaimingColumns")]
public partial class SyncOutboxClaimingColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'ClaimedBy')
                 AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'claimed_by')
              THEN ALTER TABLE "OutboxMessages" RENAME COLUMN "ClaimedBy" TO claimed_by; END IF;
              IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'NextAttemptAt')
                 AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'next_attempt_at')
              THEN ALTER TABLE "OutboxMessages" RENAME COLUMN "NextAttemptAt" TO next_attempt_at; END IF;
              IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'DeadLetteredOn')
                 AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'dead_lettered_on')
              THEN ALTER TABLE "OutboxMessages" RENAME COLUMN "DeadLetteredOn" TO dead_lettered_on; END IF;
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'claimed_by')
              THEN ALTER TABLE "OutboxMessages" ADD COLUMN claimed_by varchar(100); END IF;
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'next_attempt_at')
              THEN ALTER TABLE "OutboxMessages" ADD COLUMN next_attempt_at timestamptz; END IF;
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = 'dead_lettered_on')
              THEN ALTER TABLE "OutboxMessages" ADD COLUMN dead_lettered_on timestamptz; END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
