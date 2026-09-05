using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityInspectionPlanVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_inspection_plan_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SamplingMethod = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SamplingFrequency = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AcceptanceCriteria = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_inspection_plan_versions", x => x.Id);
                    table.CheckConstraint("CK_manufacturing_inspection_plan_version_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveFrom\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_inspection_plan_versions_TenantKey_PlanCode_V~",
                table: "manufacturing_inspection_plan_versions",
                columns: new[] { "TenantKey", "PlanCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_inspection_plan_versions_TenantKey_ProductSku~",
                table: "manufacturing_inspection_plan_versions",
                columns: new[] { "TenantKey", "ProductSku", "Status", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_inspection_plan_versions");
        }
    }
}
