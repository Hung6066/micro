using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseAndStorageLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_warehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_warehouses_manufacturing_facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "manufacturing_facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_storage_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_storage_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_storage_locations_manufacturing_warehouses_Wa~",
                        column: x => x.WarehouseId,
                        principalTable: "manufacturing_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_storage_locations_TenantKey_WarehouseId_Code",
                table: "manufacturing_storage_locations",
                columns: new[] { "TenantKey", "WarehouseId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_storage_locations_WarehouseId",
                table: "manufacturing_storage_locations",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_warehouses_FacilityId",
                table: "manufacturing_warehouses",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_warehouses_TenantKey_Code",
                table: "manufacturing_warehouses",
                columns: new[] { "TenantKey", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_storage_locations");

            migrationBuilder.DropTable(
                name: "manufacturing_warehouses");
        }
    }
}
