using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations;

public partial class AddMachineDowntimes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "manufacturing_machine_downtimes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                ProductionBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                OperationExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_manufacturing_machine_downtimes", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_manufacturing_machine_downtimes_TenantKey_MachineId_Status",
            table: "manufacturing_machine_downtimes",
            columns: new[] { "TenantKey", "MachineId", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_manufacturing_machine_downtimes_StartedAt",
            table: "manufacturing_machine_downtimes",
            column: "StartedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "manufacturing_machine_downtimes");
    }
}
