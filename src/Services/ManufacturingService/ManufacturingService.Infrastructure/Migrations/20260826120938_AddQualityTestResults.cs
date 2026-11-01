using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityTestResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpecificationReference",
                table: "manufacturing_quality_inspections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "manufacturing_quality_test_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MeasuredValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Uom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LowerLimit = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    UpperLimit = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Method = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_quality_test_results", x => x.Id);
                    table.CheckConstraint("CK_manufacturing_quality_test_result_status", "\"Result\" IN ('Pass', 'Fail', 'NotApplicable')");
                    table.ForeignKey(
                        name: "FK_manufacturing_quality_test_results_manufacturing_quality_in~",
                        column: x => x.QualityInspectionId,
                        principalTable: "manufacturing_quality_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_quality_test_results_QualityInspectionId_Test~",
                table: "manufacturing_quality_test_results",
                columns: new[] { "QualityInspectionId", "TestCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_quality_test_results");

            migrationBuilder.DropColumn(
                name: "SpecificationReference",
                table: "manufacturing_quality_inspections");
        }
    }
}
