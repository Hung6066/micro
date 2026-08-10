using His.Hope.AgentHarness.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.AgentHarness.Infrastructure.Persistence.Migrations;

/// <summary>
/// Corrective migration: ensures eval_suites and eval_runs tables exist on databases
/// that may have been initialized with partial migrations (e.g. EnsureCreated +
/// later selective Migrate calls). EF Core's GetPendingMigrations computes pending
/// migrations as the set difference of all available minus applied history entries,
/// then sorts by timestamp — so an earlier-timestamp migration IS applied once
/// discovered. The defense here is against edge cases where the history entry exists
/// but the tables were manually dropped or the DB was created out of band.
///
/// Uses CREATE TABLE IF NOT EXISTS so it is idempotent on fresh databases where
/// 20260720090000_AddEvalTables already created the tables.
///
/// No designer partial exists for this migration; attributes are applied directly.
/// </summary>
[DbContext(typeof(HarnessDbContext))]
[Migration("20260720210000_FixAddEvalTables")]
public partial class FixAddEvalTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent creation of eval_suites — no-op if already exists
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS harness.eval_suites (
                id uuid NOT NULL,
                name character varying(256) NOT NULL,
                domain character varying(128) NOT NULL,
                description text NOT NULL,
                definition_json jsonb NOT NULL,
                created_at timestamp with time zone NOT NULL,
                CONSTRAINT pk_eval_suites PRIMARY KEY (id)
            );
            """);

        // Idempotent creation of eval_runs — no-op if already exists
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS harness.eval_runs (
                id uuid NOT NULL,
                eval_suite_id uuid NOT NULL,
                target_agent character varying(128) NOT NULL,
                target_model character varying(128) NULL,
                pass_at_1 double precision NULL,
                pass_at_k double precision NULL,
                judge_score integer NULL,
                status character varying(20) NOT NULL,
                started_at timestamp with time zone NOT NULL,
                completed_at timestamp with time zone NULL,
                raw_result_json jsonb NULL,
                CONSTRAINT pk_eval_runs PRIMARY KEY (id)
            );
            """);

        // FK constraint — only add if not already present
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE constraint_schema = 'harness'
                      AND constraint_name = 'fk_eval_runs_suite_id'
                ) THEN
                    ALTER TABLE harness.eval_runs
                        ADD CONSTRAINT fk_eval_runs_suite_id
                        FOREIGN KEY (eval_suite_id) REFERENCES harness.eval_suites(id)
                        ON DELETE CASCADE;
                END IF;
            END $$;
            """);

        // Indexes — IF NOT EXISTS is supported natively
        migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ix_eval_suites_name ON harness.eval_suites (name)");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_eval_runs_suite_id ON harness.eval_runs (eval_suite_id)");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_eval_runs_target_agent ON harness.eval_runs (target_agent)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // INTENTIONAL NO-OP: This is a corrective/idempotent migration for environments
        // where 20260720090000_AddEvalTables was never applied. Rolling back must NOT
        // drop the eval_* tables because they may belong to that earlier migration on
        // databases that HAVE applied it. A no-op Down ensures rollback safety regardless
        // of which state the database is in.
    }
}
