using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations;

public partial class LinkQualityInspectionsToPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "InspectionPlanVersionId", table: "manufacturing_quality_inspections", type: "uuid", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_manufacturing_quality_inspections_InspectionPlanVersionId", table: "manufacturing_quality_inspections", column: "InspectionPlanVersionId");
        migrationBuilder.AddForeignKey(name: "FK_manufacturing_quality_inspections_manufacturing_inspection_plan_versions_InspectionPlanVersionId", table: "manufacturing_quality_inspections", column: "InspectionPlanVersionId", principalTable: "manufacturing_inspection_plan_versions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_manufacturing_quality_inspections_manufacturing_inspection_plan_versions_InspectionPlanVersionId", table: "manufacturing_quality_inspections");
        migrationBuilder.DropIndex(name: "IX_manufacturing_quality_inspections_InspectionPlanVersionId", table: "manufacturing_quality_inspections");
        migrationBuilder.DropColumn(name: "InspectionPlanVersionId", table: "manufacturing_quality_inspections");
    }
}
