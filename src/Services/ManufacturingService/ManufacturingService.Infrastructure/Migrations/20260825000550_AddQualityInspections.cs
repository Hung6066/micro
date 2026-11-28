using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityInspections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_quality_inspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MoisturePercent = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    Inspector = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InspectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_quality_inspections", x => x.Id);
                    table.CheckConstraint("CK_manufacturing_quality_moisture_range", "\"MoisturePercent\" >= 0 AND \"MoisturePercent\" <= 100");
                    table.ForeignKey(
                        name: "FK_manufacturing_quality_inspections_manufacturing_lots_LotId",
                        column: x => x.LotId,
                        principalTable: "manufacturing_lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_quality_inspections_LotId",
                table: "manufacturing_quality_inspections",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_quality_inspections_TenantKey_LotId_Inspected~",
                table: "manufacturing_quality_inspections",
                columns: new[] { "TenantKey", "LotId", "InspectedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_quality_inspections");
        }
    }
}
