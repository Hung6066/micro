using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.AgentHarness.Infrastructure.Persistence.Migrations;

public partial class AddEvalTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "eval_suites",
            schema: "harness",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                domain = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                definition_json = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_eval_suites", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "eval_runs",
            schema: "harness",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                eval_suite_id = table.Column<Guid>(type: "uuid", nullable: false),
                target_agent = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                target_model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                pass_at_1 = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                pass_at_k = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                judge_score = table.Column<int>(type: "integer", nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                raw_result_json = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_eval_runs", x => x.id);
                table.ForeignKey(
                    name: "fk_eval_runs_suite_id",
                    column: x => x.eval_suite_id,
                    principalTable: "eval_suites",
                    principalSchema: "harness",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_eval_suites_name",
            schema: "harness",
            table: "eval_suites",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_eval_runs_suite_id",
            schema: "harness",
            table: "eval_runs",
            column: "eval_suite_id");

        migrationBuilder.CreateIndex(
            name: "ix_eval_runs_target_agent",
            schema: "harness",
            table: "eval_runs",
            column: "target_agent");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "eval_runs", schema: "harness");
        migrationBuilder.DropTable(name: "eval_suites", schema: "harness");
    }
}
