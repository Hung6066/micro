using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddManufacturingMachines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_machines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LastMaintenanceAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextMaintenanceAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_machines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_machines_TenantKey_Code",
                table: "manufacturing_machines",
                columns: new[] { "TenantKey", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_machines");
        }
    }
}
