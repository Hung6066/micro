using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.AgentHarness.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryEntryConfidenceScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "confidence_score",
                schema: "harness",
                table: "memory_entries",
                type: "numeric(4,3)",
                precision: 4,
                scale: 3,
                nullable: false,
                defaultValue: 0.85m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confidence_score",
                schema: "harness",
                table: "memory_entries");
        }
    }
}
