using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.ClinicalService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "facility_id",
                table: "encounters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_encounters_facility_id",
                table: "encounters",
                column: "facility_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_encounters_facility_id",
                table: "encounters");

            migrationBuilder.DropColumn(
                name: "facility_id",
                table: "encounters");
        }
    }
}
