using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations
{
    /// <inheritdoc />
    public partial class LinkTransformationsToMachines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MachineId",
                table: "manufacturing_transformations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_transformations_MachineId",
                table: "manufacturing_transformations",
                column: "MachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_manufacturing_transformations_manufacturing_machines_Machin~",
                table: "manufacturing_transformations",
                column: "MachineId",
                principalTable: "manufacturing_machines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_manufacturing_transformations_manufacturing_machines_Machin~",
                table: "manufacturing_transformations");

            migrationBuilder.DropIndex(
                name: "IX_manufacturing_transformations_MachineId",
                table: "manufacturing_transformations");

            migrationBuilder.DropColumn(
                name: "MachineId",
                table: "manufacturing_transformations");
        }
    }
}
