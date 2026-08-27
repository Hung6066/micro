using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations;

public partial class AddMaintenancePlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "manufacturing_maintenance_plans", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            MachineId = table.Column<Guid>(type: "uuid", nullable: false),
            TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            PlanCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            MaintenanceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
            FrequencyDays = table.Column<int>(type: "integer", nullable: false),
            NextDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Checklist = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
            AssignedTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
            Active = table.Column<bool>(type: "boolean", nullable: false),
            LastGeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_manufacturing_maintenance_plans", x => x.Id);
            table.CheckConstraint("CK_manufacturing_maintenance_plan_frequency", "\"FrequencyDays\" > 0");
            table.ForeignKey("FK_manufacturing_maintenance_plans_manufacturing_machines_MachineId", x => x.MachineId, "manufacturing_machines", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_manufacturing_maintenance_plans_TenantKey_MachineId_PlanCode", table: "manufacturing_maintenance_plans", columns: new[] { "TenantKey", "MachineId", "PlanCode" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_manufacturing_maintenance_plans_TenantKey_Active_NextDueAt", table: "manufacturing_maintenance_plans", columns: new[] { "TenantKey", "Active", "NextDueAt" });
        migrationBuilder.CreateIndex(name: "IX_manufacturing_maintenance_plans_MachineId", table: "manufacturing_maintenance_plans", column: "MachineId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "manufacturing_maintenance_plans");
}
