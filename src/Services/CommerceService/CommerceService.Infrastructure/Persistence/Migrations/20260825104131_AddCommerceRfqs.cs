using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommerceRfqs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "commerce_rfqs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BuyerUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    QuotedTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    OperatorNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commerce_rfqs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "commerce_rfq_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commerce_rfq_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_commerce_rfq_lines_commerce_rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "commerce_rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_commerce_rfq_lines_RfqId_ProductId",
                table: "commerce_rfq_lines",
                columns: new[] { "RfqId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_commerce_rfqs_TenantKey_BuyerUserId_CreatedAt",
                table: "commerce_rfqs",
                columns: new[] { "TenantKey", "BuyerUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commerce_rfq_lines");

            migrationBuilder.DropTable(
                name: "commerce_rfqs");
        }
    }
}
