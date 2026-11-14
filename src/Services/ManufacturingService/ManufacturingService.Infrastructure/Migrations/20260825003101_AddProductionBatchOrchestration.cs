using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionBatchOrchestration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_production_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersion = table.Column<int>(type: "integer", nullable: false),
                    TargetQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    OutputUom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_production_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_production_orders_manufacturing_recipes_Recip~",
                        column: x => x.RecipeId,
                        principalTable: "manufacturing_recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_production_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ActualOutputQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_production_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_production_batches_manufacturing_machines_Mac~",
                        column: x => x.MachineId,
                        principalTable: "manufacturing_machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manufacturing_production_batches_manufacturing_production_o~",
                        column: x => x.ProductionOrderId,
                        principalTable: "manufacturing_production_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_operation_executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ProcessStep = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Operator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InputQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    OutputQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    LossQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    QcStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_operation_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_operation_executions_manufacturing_production~",
                        column: x => x.ProductionBatchId,
                        principalTable: "manufacturing_production_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_operation_executions_ProductionBatchId_Sequen~",
                table: "manufacturing_operation_executions",
                columns: new[] { "ProductionBatchId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_production_batches_MachineId",
                table: "manufacturing_production_batches",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_production_batches_ProductionOrderId",
                table: "manufacturing_production_batches",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_production_batches_TenantKey_BatchNumber",
                table: "manufacturing_production_batches",
                columns: new[] { "TenantKey", "BatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_production_orders_RecipeId",
                table: "manufacturing_production_orders",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_production_orders_TenantKey_OrderNumber",
                table: "manufacturing_production_orders",
                columns: new[] { "TenantKey", "OrderNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_operation_executions");

            migrationBuilder.DropTable(
                name: "manufacturing_production_batches");

            migrationBuilder.DropTable(
                name: "manufacturing_production_orders");
        }
    }
}
