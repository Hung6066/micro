using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMlTrainingData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_ml_feature_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DatasetKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOf = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FeaturesJson = table.Column<string>(type: "text", nullable: false),
                    LabelJson = table.Column<string>(type: "text", nullable: true),
                    SourceEventIdsJson = table.Column<string>(type: "text", nullable: true),
                    Split = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_ml_feature_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_operation_measurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: true),
                    LotId = table.Column<Guid>(type: "uuid", nullable: true),
                    MeasurementType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Uom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MeasuredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_operation_measurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_operation_measurements_manufacturing_producti~",
                        column: x => x.ProductionBatchId,
                        principalTable: "manufacturing_production_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_sales_actuals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Uom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Channel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_sales_actuals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_ml_feature_snapshots_TenantKey_DatasetKey_AsOf",
                table: "manufacturing_ml_feature_snapshots",
                columns: new[] { "TenantKey", "DatasetKey", "AsOf" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_ml_feature_snapshots_TenantKey_DatasetKey_Ent~",
                table: "manufacturing_ml_feature_snapshots",
                columns: new[] { "TenantKey", "DatasetKey", "EntityType", "EntityId", "AsOf", "SchemaVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_operation_measurements_ProductionBatchId",
                table: "manufacturing_operation_measurements",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_operation_measurements_TenantKey_OperationExe~",
                table: "manufacturing_operation_measurements",
                columns: new[] { "TenantKey", "OperationExecutionId", "MeasurementType", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_operation_measurements_TenantKey_ProductionBa~",
                table: "manufacturing_operation_measurements",
                columns: new[] { "TenantKey", "ProductionBatchId", "MeasuredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_sales_actuals_TenantKey_ProductSku_PeriodStar~",
                table: "manufacturing_sales_actuals",
                columns: new[] { "TenantKey", "ProductSku", "PeriodStart", "Channel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_ml_feature_snapshots");

            migrationBuilder.DropTable(
                name: "manufacturing_operation_measurements");

            migrationBuilder.DropTable(
                name: "manufacturing_sales_actuals");
        }
    }
}
