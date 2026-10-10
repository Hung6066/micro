using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

[DbContext(typeof(ManufacturingDbContext))]
[Migration("20260825030000_AddMaintenanceWorkOrders")]
public partial class AddMaintenanceWorkOrders : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "manufacturing_maintenance_work_orders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                MaintenanceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AssignedTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Technician = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Evidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_manufacturing_maintenance_work_orders", x => x.Id);
                table.ForeignKey("FK_manufacturing_maintenance_work_orders_manufacturing_machines_MachineId", x => x.MachineId, "manufacturing_machines", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_manufacturing_maintenance_work_orders_TenantKey_MachineId_Status", "manufacturing_maintenance_work_orders", new[] { "TenantKey", "MachineId", "Status" });
        migrationBuilder.CreateIndex("IX_manufacturing_maintenance_work_orders_DueAt", "manufacturing_maintenance_work_orders", "DueAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("manufacturing_maintenance_work_orders");
}
