using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.LabService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncOutboxClaimingColumnsGenerated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddOrRenameColumn(migrationBuilder, "ClaimedBy", "claimed_by", "character varying(100)");
            AddOrRenameColumn(migrationBuilder, "NextAttemptAt", "next_attempt_at", "timestamp with time zone");
            AddOrRenameColumn(migrationBuilder, "DeadLetteredOn", "dead_lettered_on", "timestamp with time zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"OutboxMessages\" DROP COLUMN IF EXISTS claimed_by");
            migrationBuilder.Sql("ALTER TABLE \"OutboxMessages\" DROP COLUMN IF EXISTS next_attempt_at");
            migrationBuilder.Sql("ALTER TABLE \"OutboxMessages\" DROP COLUMN IF EXISTS dead_lettered_on");
        }

        private static void AddOrRenameColumn(MigrationBuilder migrationBuilder, string legacyName, string currentName, string sqlType)
        {
            migrationBuilder.Sql($"DO $$ BEGIN IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = '{legacyName}') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = '{currentName}') THEN ALTER TABLE \"OutboxMessages\" RENAME COLUMN \"{legacyName}\" TO {currentName}; ELSIF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OutboxMessages' AND column_name = '{currentName}') THEN ALTER TABLE \"OutboxMessages\" ADD COLUMN {currentName} {sqlType}; END IF; END $$;");
        }
    }
}
