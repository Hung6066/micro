using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTransformationInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_manufacturing_transformations_loss_non_negative",
                table: "manufacturing_transformations",
                sql: "\"LossQuantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manufacturing_transformations_output_le_input",
                table: "manufacturing_transformations",
                sql: "\"OutputQuantity\" <= \"InputQuantity\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manufacturing_transformations_yield_range",
                table: "manufacturing_transformations",
                sql: "\"YieldPercent\" >= 0 AND \"YieldPercent\" <= 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_manufacturing_transformations_loss_non_negative",
                table: "manufacturing_transformations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manufacturing_transformations_output_le_input",
                table: "manufacturing_transformations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manufacturing_transformations_yield_range",
                table: "manufacturing_transformations");
        }
    }
}
