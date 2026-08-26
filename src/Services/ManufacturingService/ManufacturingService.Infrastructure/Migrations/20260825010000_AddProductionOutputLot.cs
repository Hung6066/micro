using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations;

public partial class AddProductionOutputLot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "OutputLotId",
            table: "manufacturing_production_batches",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_manufacturing_production_batches_OutputLotId",
            table: "manufacturing_production_batches",
            column: "OutputLotId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_manufacturing_production_batches_OutputLotId",
            table: "manufacturing_production_batches");

        migrationBuilder.DropColumn(
            name: "OutputLotId",
            table: "manufacturing_production_batches");
    }
}
