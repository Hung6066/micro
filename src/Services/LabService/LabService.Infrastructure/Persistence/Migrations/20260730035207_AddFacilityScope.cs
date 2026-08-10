using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.LabService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "facilityid",
                table: "LabOrders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_facilityid",
                table: "LabOrders",
                column: "facilityid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LabOrders_facilityid",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "facilityid",
                table: "LabOrders");
        }
    }
}
