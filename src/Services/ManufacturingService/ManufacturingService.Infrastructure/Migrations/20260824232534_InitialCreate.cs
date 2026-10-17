using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_lots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Uom = table.Column<string>(type: "text", nullable: false),
                    Disposition = table.Column<string>(type: "text", nullable: false),
                    BestBefore = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_lots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_transformations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "text", nullable: false),
                    ProcessStep = table.Column<string>(type: "text", nullable: false),
                    OutputLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    OutputQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    YieldPercent = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    LossQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_transformations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_transformation_inputs",
                columns: table => new
                {
                    TransformationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_transformation_inputs", x => new { x.TransformationId, x.LotId });
                    table.ForeignKey(
                        name: "FK_manufacturing_transformation_inputs_manufacturing_transform~",
                        column: x => x.TransformationId,
                        principalTable: "manufacturing_transformations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_lots_TenantKey_Sku_Disposition",
                table: "manufacturing_lots",
                columns: new[] { "TenantKey", "Sku", "Disposition" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_outbox_messages_Status_OccurredOn",
                table: "manufacturing_outbox_messages",
                columns: new[] { "Status", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_transformation_inputs_LotId",
                table: "manufacturing_transformation_inputs",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_transformations_OutputLotId",
                table: "manufacturing_transformations",
                column: "OutputLotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_lots");

            migrationBuilder.DropTable(
                name: "manufacturing_outbox_messages");

            migrationBuilder.DropTable(
                name: "manufacturing_transformation_inputs");

            migrationBuilder.DropTable(
                name: "manufacturing_transformations");
        }
    }
}
