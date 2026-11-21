using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLotReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_lot_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Uom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_lot_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_lot_reservations_manufacturing_lots_LotId",
                        column: x => x.LotId,
                        principalTable: "manufacturing_lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_lot_reservations_LotId",
                table: "manufacturing_lot_reservations",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_lot_reservations_TenantKey_LotId_Status",
                table: "manufacturing_lot_reservations",
                columns: new[] { "TenantKey", "LotId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_lot_reservations_TenantKey_ReferenceType_Refe~",
                table: "manufacturing_lot_reservations",
                columns: new[] { "TenantKey", "ReferenceType", "ReferenceId", "LotId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_lot_reservations");
        }
    }
}
