using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierRfqsAndQuotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_supplier_rfqs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RfqNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Uom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NeededBy = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_supplier_rfqs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_supplier_quotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierRfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_supplier_quotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_supplier_quotations_manufacturing_supplier_rf~",
                        column: x => x.SupplierRfqId,
                        principalTable: "manufacturing_supplier_rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_manufacturing_supplier_quotations_manufacturing_suppliers_S~",
                        column: x => x.SupplierId,
                        principalTable: "manufacturing_suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_supplier_quotations_SupplierId",
                table: "manufacturing_supplier_quotations",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_supplier_quotations_SupplierRfqId",
                table: "manufacturing_supplier_quotations",
                column: "SupplierRfqId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_supplier_quotations_TenantKey_SupplierRfqId_S~",
                table: "manufacturing_supplier_quotations",
                columns: new[] { "TenantKey", "SupplierRfqId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_supplier_rfqs_TenantKey_RfqNumber",
                table: "manufacturing_supplier_rfqs",
                columns: new[] { "TenantKey", "RfqNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_supplier_quotations");

            migrationBuilder.DropTable(
                name: "manufacturing_supplier_rfqs");
        }
    }
}
