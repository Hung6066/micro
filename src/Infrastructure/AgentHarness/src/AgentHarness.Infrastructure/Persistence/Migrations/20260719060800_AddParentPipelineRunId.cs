using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.AgentHarness.Infrastructure.Persistence.Migrations;

public partial class AddParentPipelineRunId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "parent_pipeline_run_id",
            schema: "harness",
            table: "pipeline_runs",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_pipeline_runs_parent_id",
            schema: "harness",
            table: "pipeline_runs",
            column: "parent_pipeline_run_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_pipeline_runs_parent_id",
            schema: "harness",
            table: "pipeline_runs");

        migrationBuilder.DropColumn(
            name: "parent_pipeline_run_id",
            schema: "harness",
            table: "pipeline_runs");
    }
}
