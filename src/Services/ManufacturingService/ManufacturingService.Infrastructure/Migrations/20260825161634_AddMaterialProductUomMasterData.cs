using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialProductUomMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_uom_conversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Factor = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_uom_conversions", x => x.Id);
                    table.CheckConstraint("CK_manufacturing_uom_conversion_factor_positive", "\"Factor\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_uoms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Dimension = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_uoms", x => x.Id);
                    table.UniqueConstraint("AK_manufacturing_uoms_Code", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_materials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BaseUomCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MaterialType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_materials_manufacturing_uoms_BaseUomCode",
                        column: x => x.BaseUomCode,
                        principalTable: "manufacturing_uoms",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BaseUomCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_products_manufacturing_uoms_BaseUomCode",
                        column: x => x.BaseUomCode,
                        principalTable: "manufacturing_uoms",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_materials_BaseUomCode",
                table: "manufacturing_materials",
                column: "BaseUomCode");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_materials_TenantKey_Sku",
                table: "manufacturing_materials",
                columns: new[] { "TenantKey", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_products_BaseUomCode",
                table: "manufacturing_products",
                column: "BaseUomCode");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_products_TenantKey_Sku",
                table: "manufacturing_products",
                columns: new[] { "TenantKey", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_uom_conversions_FromCode_ToCode",
                table: "manufacturing_uom_conversions",
                columns: new[] { "FromCode", "ToCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_uoms_Code",
                table: "manufacturing_uoms",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_materials");

            migrationBuilder.DropTable(
                name: "manufacturing_products");

            migrationBuilder.DropTable(
                name: "manufacturing_uom_conversions");

            migrationBuilder.DropTable(
                name: "manufacturing_uoms");
        }
    }
}
