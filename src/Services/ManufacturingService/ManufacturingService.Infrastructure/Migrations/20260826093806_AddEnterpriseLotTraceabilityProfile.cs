using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseLotTraceabilityProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CertificateOfAnalysisReference",
                table: "manufacturing_lots",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "manufacturing_lots",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FacilityCode",
                table: "manufacturing_lots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotCode",
                table: "manufacturing_lots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LotType",
                table: "manufacturing_lots",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ManufacturedOn",
                table: "manufacturing_lots",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginCountryCode",
                table: "manufacturing_lots",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityStatus",
                table: "manufacturing_lots",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReceivedAt",
                table: "manufacturing_lots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceLotCode",
                table: "manufacturing_lots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageLocationCode",
                table: "manufacturing_lots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "manufacturing_lots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedQuantity",
                table: "manufacturing_inbound_receipts",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CarrierName",
                table: "manufacturing_inbound_receipts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateOfAnalysisReference",
                table: "manufacturing_inbound_receipts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNoteNumber",
                table: "manufacturing_inbound_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivedBy",
                table: "manufacturing_inbound_receipts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RejectedQuantity",
                table: "manufacturing_inbound_receipts",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StorageLocationCode",
                table: "manufacturing_inbound_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TemperatureOnReceiptC",
                table: "manufacturing_inbound_receipts",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleReference",
                table: "manufacturing_inbound_receipts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "manufacturing_lot_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromDisposition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToDisposition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_lot_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_lot_status_history_manufacturing_lots_LotId",
                        column: x => x.LotId,
                        principalTable: "manufacturing_lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                UPDATE manufacturing_lots
                SET "LotCode" = CONCAT('LEGACY-', "Id"::text),
                    "LotType" = CASE
                        WHEN "Sku" LIKE 'RM-%' THEN 'RawMaterial'
                        WHEN "Sku" LIKE 'FG-%' OR "Sku" LIKE 'FX-%' THEN 'FinishedGood'
                        ELSE 'Unspecified'
                    END,
                    "QualityStatus" = CASE WHEN "Disposition" = 'Released' THEN 'Passed' ELSE 'Pending' END,
                    "CreatedBy" = 'migration'
                WHERE "LotCode" = '' OR "LotType" = '' OR "QualityStatus" = '' OR "CreatedBy" = '';
                """);

            migrationBuilder.Sql("""
                UPDATE manufacturing_inbound_receipts
                SET "AcceptedQuantity" = "Quantity",
                    "RejectedQuantity" = 0
                WHERE "AcceptedQuantity" = 0 AND "RejectedQuantity" = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_lots_TenantKey_LotCode",
                table: "manufacturing_lots",
                columns: new[] { "TenantKey", "LotCode" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_manufacturing_lots_lot_type",
                table: "manufacturing_lots",
                sql: "\"LotType\" IN ('RawMaterial', 'WorkInProgress', 'FinishedGood', 'Packaging', 'Unspecified')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manufacturing_inbound_receipts_quantity_balance",
                table: "manufacturing_inbound_receipts",
                sql: "\"AcceptedQuantity\" + \"RejectedQuantity\" = \"Quantity\"");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_lot_status_history_LotId",
                table: "manufacturing_lot_status_history",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_lot_status_history_TenantKey_LotId_OccurredAt",
                table: "manufacturing_lot_status_history",
                columns: new[] { "TenantKey", "LotId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_lot_status_history");

            migrationBuilder.DropIndex(
                name: "IX_manufacturing_lots_TenantKey_LotCode",
                table: "manufacturing_lots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manufacturing_lots_lot_type",
                table: "manufacturing_lots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manufacturing_inbound_receipts_quantity_balance",
                table: "manufacturing_inbound_receipts");

            migrationBuilder.DropColumn(
                name: "CertificateOfAnalysisReference",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "FacilityCode",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "LotCode",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "LotType",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "ManufacturedOn",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "OriginCountryCode",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "QualityStatus",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "SourceLotCode",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "StorageLocationCode",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "manufacturing_lots");

            migrationBuilder.DropColumn(
                name: "AcceptedQuantity",
                table: "manufacturing_inbound_receipts");

            migrationBuilder.DropColumn(
                name: "CarrierName",
                table: "manufacturing_inbound_receipts");

            migrationBuilder.DropColumn(
                name: "CertificateOfAnalysisReference",
                table: "manufacturing_inbound_receipts");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteNumber",
                table: "manufacturing_inbound_receipts");

            migrationBuilder.DropColumn(
                name: "ReceivedBy",
                table: "manufacturing_inbound_receipts");

            migrationBuilder.DropColumn(
                name: "RejectedQuantity",
                table: "manufacturing_inbound_receipts");

            migrationBuilder.DropColumn(
                name: "StorageLocationCode",
                table: "manufacturing_inbound_receipts");

            migrationBuilder.DropColumn(
                name: "TemperatureOnReceiptC",
                table: "manufacturing_inbound_receipts");

            migrationBuilder.DropColumn(
                name: "VehicleReference",
                table: "manufacturing_inbound_receipts");
        }
    }
}
