using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddManufacturingRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ProcessStep = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OutputUom = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetYieldPercent = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_recipes", x => x.Id);
                    table.CheckConstraint("CK_manufacturing_recipes_yield_range", "\"TargetYieldPercent\" > 0 AND \"TargetYieldPercent\" <= 100");
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_recipes_TenantKey_ProductSku_Version",
                table: "manufacturing_recipes",
                columns: new[] { "TenantKey", "ProductSku", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_recipes");
        }
    }
}
