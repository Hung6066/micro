using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.LabService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_Facility_Status_Date_Id",
                table: "LabOrders",
                columns: new[] { "facilityid", "status", "orderdate" });

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_Patient_Date_Id",
                table: "LabOrders",
                columns: new[] { "patientid", "orderdate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LabOrders_Facility_Status_Date_Id",
                table: "LabOrders");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_Patient_Date_Id",
                table: "LabOrders");
        }
    }
}
