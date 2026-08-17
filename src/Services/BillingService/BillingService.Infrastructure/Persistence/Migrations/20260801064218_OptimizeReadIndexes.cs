using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.BillingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Facility_Status_Date_Id",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "FacilityId", "Status", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Patient_Date_Id",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "PatientId", "InvoiceDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_Facility_Status_Date_Id",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Patient_Date_Id",
                schema: "billing",
                table: "Invoices");
        }
    }
}
