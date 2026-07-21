using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace His.Hope.AgentHarness.Infrastructure.Persistence.Migrations;

public partial class InitialHarnessSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");
        migrationBuilder.EnsureSchema(name: "harness");

        migrationBuilder.CreateTable(
            name: "agent_pool",
            schema: "harness",
            columns: table => new
            {
                agent_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                available_slots = table.Column<int>(type: "integer", nullable: false),
                total_slots = table.Column<int>(type: "integer", nullable: false),
                is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                last_heartbeat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                circuit_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_agent_pool", x => x.agent_name);
            });

        migrationBuilder.CreateTable(
            name: "pipeline_runs",
            schema: "harness",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workflow_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                parameters = table.Column<string>(type: "jsonb", nullable: false),
                triggered_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                timeout_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                metadata = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pipeline_runs", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "memory_entries",
            schema: "harness",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                error_pattern = table.Column<string>(type: "text", nullable: false),
                error_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                agent_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                fix_description = table.Column<string>(type: "text", nullable: false),
                fix_artifact_ref = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                success = table.Column<bool>(type: "boolean", nullable: false),
                use_count = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                keywords = table.Column<string>(type: "text", nullable: false),
                embedding = table.Column<Vector>(type: "vector(256)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_memory_entries", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "pending_approvals",
            schema: "harness",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                action_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                requested_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                details = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                approved_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                reject_reason = table.Column<string>(type: "text", nullable: true),
                context = table.Column<string>(type: "jsonb", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pending_approvals", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "artifacts",
            schema: "harness",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                pipeline_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                storage_ref = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                size_bytes = table.Column<long>(type: "bigint", nullable: false),
                content = table.Column<byte[]>(type: "bytea", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_artifacts", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "quality_gate_results",
            schema: "harness",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                pipeline_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                gate_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                gate_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                passed = table.Column<bool>(type: "boolean", nullable: false),
                details = table.Column<string>(type: "text", nullable: true),
                evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_quality_gate_results", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "pipeline_checkpoints",
            schema: "harness",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                pipeline_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                phase = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                completed_node_ids = table.Column<string>(type: "jsonb", nullable: false),
                failed_node_ids = table.Column<string>(type: "jsonb", nullable: false),
                node_states = table.Column<string>(type: "jsonb", nullable: false),
                loop_iteration = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pipeline_checkpoints", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "agent_runs",
            schema: "harness",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                pipeline_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                agent_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                task_description = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                attempt_number = table.Column<int>(type: "integer", nullable: false),
                confidence_score = table.Column<decimal>(type: "numeric(3,2)", nullable: true),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                output_artifact_ref = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                error_message = table.Column<string>(type: "text", nullable: true),
                retry_count = table.Column<int>(type: "integer", nullable: false),
                max_retries = table.Column<int>(type: "integer", nullable: false),
                timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                circuit_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_agent_runs", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_pipeline_runs_status",
            schema: "harness",
            table: "pipeline_runs",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_agent_runs_pipeline_run_id",
            schema: "harness",
            table: "agent_runs",
            column: "pipeline_run_id");

        migrationBuilder.CreateIndex(
            name: "ix_agent_runs_agent_name",
            schema: "harness",
            table: "agent_runs",
            column: "agent_name");

        migrationBuilder.CreateIndex(
            name: "ix_quality_gate_results_pipeline_run_id",
            schema: "harness",
            table: "quality_gate_results",
            column: "pipeline_run_id");

        migrationBuilder.CreateIndex(
            name: "ix_artifacts_pipeline_run_id",
            schema: "harness",
            table: "artifacts",
            column: "pipeline_run_id");

        migrationBuilder.CreateIndex(
            name: "ix_checkpoints_pipeline_run_id",
            schema: "harness",
            table: "pipeline_checkpoints",
            column: "pipeline_run_id");

        migrationBuilder.CreateIndex(
            name: "ix_checkpoints_pipeline_run_created",
            schema: "harness",
            table: "pipeline_checkpoints",
            columns: new[] { "pipeline_run_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_memory_agent_name",
            schema: "harness",
            table: "memory_entries",
            column: "agent_name");

        migrationBuilder.CreateIndex(
            name: "ix_memory_error_category",
            schema: "harness",
            table: "memory_entries",
            column: "error_category");

        migrationBuilder.CreateIndex(
            name: "ix_pending_approvals_status",
            schema: "harness",
            table: "pending_approvals",
            column: "status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "agent_pool", schema: "harness");
        migrationBuilder.DropTable(name: "agent_runs", schema: "harness");
        migrationBuilder.DropTable(name: "artifacts", schema: "harness");
        migrationBuilder.DropTable(name: "memory_entries", schema: "harness");
        migrationBuilder.DropTable(name: "pending_approvals", schema: "harness");
        migrationBuilder.DropTable(name: "pipeline_checkpoints", schema: "harness");
        migrationBuilder.DropTable(name: "pipeline_runs", schema: "harness");
        migrationBuilder.DropTable(name: "quality_gate_results", schema: "harness");
    }
}
