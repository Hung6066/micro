using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.AppointmentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Facility_Status_Scheduled_Id",
                table: "appointments",
                columns: new[] { "facility_id", "status", "scheduled_date" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Patient_Scheduled_Id",
                table: "appointments",
                columns: new[] { "patient_id", "scheduled_date" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Provider_Scheduled_Id",
                table: "appointments",
                columns: new[] { "provider_id", "scheduled_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_Facility_Status_Scheduled_Id",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_Patient_Scheduled_Id",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_Provider_Scheduled_Id",
                table: "appointments");
        }
    }
}
