using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.PharmacyService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "facilityid",
                table: "Prescriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "facilityid",
                table: "Medications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_facilityid",
                table: "Prescriptions",
                column: "facilityid");

            migrationBuilder.CreateIndex(
                name: "IX_Medications_facilityid",
                table: "Medications",
                column: "facilityid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_facilityid",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Medications_facilityid",
                table: "Medications");

            migrationBuilder.DropColumn(
                name: "facilityid",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "facilityid",
                table: "Medications");
        }
    }
}
