using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.PatientService.Infrastructure.Projections.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "facility_id",
                table: "patient_read_models",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_read_models_facility_id",
                table: "patient_read_models",
                column: "facility_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_patient_read_models_facility_id",
                table: "patient_read_models");

            migrationBuilder.DropColumn(
                name: "facility_id",
                table: "patient_read_models");
        }
    }
}
