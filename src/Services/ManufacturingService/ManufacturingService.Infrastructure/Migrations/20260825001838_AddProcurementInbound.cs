using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementInbound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_purchase_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OrderedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_purchase_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_purchase_orders_manufacturing_suppliers_Suppl~",
                        column: x => x.SupplierId,
                        principalTable: "manufacturing_suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_purchase_order_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Uom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_purchase_order_lines", x => x.Id);
                    table.CheckConstraint("CK_manufacturing_po_lines_quantity_positive", "\"OrderedQuantity\" > 0 AND \"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"OrderedQuantity\"");
                    table.ForeignKey(
                        name: "FK_manufacturing_purchase_order_lines_manufacturing_purchase_o~",
                        column: x => x.PurchaseOrderId,
                        principalTable: "manufacturing_purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_inbound_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierLotCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FacilityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Uom = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_inbound_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_inbound_receipts_manufacturing_lots_LotId",
                        column: x => x.LotId,
                        principalTable: "manufacturing_lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manufacturing_inbound_receipts_manufacturing_purchase_order~",
                        column: x => x.PurchaseOrderId,
                        principalTable: "manufacturing_purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manufacturing_inbound_receipts_manufacturing_purchase_orde~1",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "manufacturing_purchase_order_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manufacturing_inbound_receipts_manufacturing_suppliers_Supp~",
                        column: x => x.SupplierId,
                        principalTable: "manufacturing_suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_inbound_receipts_LotId",
                table: "manufacturing_inbound_receipts",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_inbound_receipts_PurchaseOrderId",
                table: "manufacturing_inbound_receipts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_inbound_receipts_PurchaseOrderLineId",
                table: "manufacturing_inbound_receipts",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_inbound_receipts_SupplierId",
                table: "manufacturing_inbound_receipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_inbound_receipts_TenantKey_ReceiptNumber",
                table: "manufacturing_inbound_receipts",
                columns: new[] { "TenantKey", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_inbound_receipts_TenantKey_SupplierId_Supplie~",
                table: "manufacturing_inbound_receipts",
                columns: new[] { "TenantKey", "SupplierId", "SupplierLotCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_purchase_order_lines_PurchaseOrderId",
                table: "manufacturing_purchase_order_lines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_purchase_orders_SupplierId",
                table: "manufacturing_purchase_orders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_purchase_orders_TenantKey_OrderNumber",
                table: "manufacturing_purchase_orders",
                columns: new[] { "TenantKey", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_suppliers_TenantKey_Code",
                table: "manufacturing_suppliers",
                columns: new[] { "TenantKey", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_inbound_receipts");

            migrationBuilder.DropTable(
                name: "manufacturing_purchase_order_lines");

            migrationBuilder.DropTable(
                name: "manufacturing_purchase_orders");

            migrationBuilder.DropTable(
                name: "manufacturing_suppliers");
        }
    }
}
