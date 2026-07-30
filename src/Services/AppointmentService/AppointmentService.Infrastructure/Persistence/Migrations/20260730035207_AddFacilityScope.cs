using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.AppointmentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "facility_id",
                table: "appointments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_FacilityId",
                table: "appointments",
                column: "facility_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_FacilityId",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "facility_id",
                table: "appointments");
        }
    }
}
