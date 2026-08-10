using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.PharmacyService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_Facility_Status_Date_Id",
                table: "Prescriptions",
                columns: new[] { "facilityid", "status", "prescribeddate" });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_Patient_Date_Id",
                table: "Prescriptions",
                columns: new[] { "patientid", "prescribeddate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_Facility_Status_Date_Id",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_Patient_Date_Id",
                table: "Prescriptions");
        }
    }
}
