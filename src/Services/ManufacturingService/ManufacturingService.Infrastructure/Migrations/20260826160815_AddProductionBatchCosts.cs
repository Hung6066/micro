using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations;

public partial class AddProductionBatchCosts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "manufacturing_production_batch_costs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProductionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                MaterialCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                LaborCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                OverheadCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                LossCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                TotalCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                CostPerOutputUnit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CalculatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_manufacturing_production_batch_costs", x => x.Id);
                table.ForeignKey("FK_manufacturing_production_batch_costs_manufacturing_production_batches_ProductionBatchId", x => x.ProductionBatchId, "manufacturing_production_batches", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_manufacturing_production_batch_costs_TenantKey_ProductionBatchId", "manufacturing_production_batch_costs", new[] { "TenantKey", "ProductionBatchId" }, unique: true);
        migrationBuilder.CreateIndex("IX_manufacturing_production_batch_costs_ProductionBatchId", "manufacturing_production_batch_costs", "ProductionBatchId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "manufacturing_production_batch_costs");
}
