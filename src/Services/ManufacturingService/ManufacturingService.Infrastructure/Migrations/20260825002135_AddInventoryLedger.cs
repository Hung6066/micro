using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_inventory_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Uom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FacilityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StockStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_inventory_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_inventory_transactions_manufacturing_lots_Lot~",
                        column: x => x.LotId,
                        principalTable: "manufacturing_lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_inventory_transactions_LotId",
                table: "manufacturing_inventory_transactions",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_inventory_transactions_TenantKey_LotId_Occurr~",
                table: "manufacturing_inventory_transactions",
                columns: new[] { "TenantKey", "LotId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_inventory_transactions_TransactionType_Correl~",
                table: "manufacturing_inventory_transactions",
                columns: new[] { "TransactionType", "CorrelationId", "LotId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_inventory_transactions");
        }
    }
}
