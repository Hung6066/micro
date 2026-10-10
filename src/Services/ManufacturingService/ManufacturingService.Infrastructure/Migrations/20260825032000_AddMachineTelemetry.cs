using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

[DbContext(typeof(ManufacturingDbContext))]
[Migration("20260825032000_AddMachineTelemetry")]
public partial class AddMachineTelemetry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "manufacturing_machine_telemetry",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                MeterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                MeterValue = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                Sequence = table.Column<long>(type: "bigint", nullable: true),
                ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_manufacturing_machine_telemetry", x => x.Id);
                table.ForeignKey("FK_manufacturing_machine_telemetry_manufacturing_machines_MachineId", x => x.MachineId, "manufacturing_machines", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_manufacturing_machine_telemetry_TenantKey_EventId", "manufacturing_machine_telemetry", new[] { "TenantKey", "EventId" }, unique: true);
        migrationBuilder.CreateIndex("IX_manufacturing_machine_telemetry_TenantKey_MachineId_ObservedAt", "manufacturing_machine_telemetry", new[] { "TenantKey", "MachineId", "ObservedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("manufacturing_machine_telemetry");
}
