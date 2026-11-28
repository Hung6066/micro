using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations;

public partial class AddProductionBatchInputs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "manufacturing_production_batch_inputs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProductionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                LotId = table.Column<Guid>(type: "uuid", nullable: false),
                ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_manufacturing_production_batch_inputs", x => x.Id);
                table.ForeignKey("FK_manufacturing_production_batch_inputs_lots_LotId", x => x.LotId, "manufacturing_lots", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_manufacturing_production_batch_inputs_batches_ProductionBatchId", x => x.ProductionBatchId, "manufacturing_production_batches", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_manufacturing_production_batch_inputs_reservations_ReservationId", x => x.ReservationId, "manufacturing_lot_reservations", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_manufacturing_production_batch_inputs_LotId", "manufacturing_production_batch_inputs", "LotId");
        migrationBuilder.CreateIndex("IX_manufacturing_production_batch_inputs_ProductionBatchId_LotId", "manufacturing_production_batch_inputs", new[] { "ProductionBatchId", "LotId" }, unique: true);
        migrationBuilder.CreateIndex("IX_manufacturing_production_batch_inputs_ReservationId", "manufacturing_production_batch_inputs", "ReservationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("manufacturing_production_batch_inputs");
}
