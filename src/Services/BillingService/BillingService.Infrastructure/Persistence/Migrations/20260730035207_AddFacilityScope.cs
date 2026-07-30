using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.BillingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacilityId",
                schema: "billing",
                table: "Invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_FacilityId",
                schema: "billing",
                table: "Invoices",
                column: "FacilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_FacilityId",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                schema: "billing",
                table: "Invoices");
        }
    }
}
