using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations;

public partial class AddMachineCalibration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "manufacturing_machine_calibrations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                CalibrationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                CertificateNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CalibratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                NextDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Result = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_manufacturing_machine_calibrations", x => x.Id);
                table.CheckConstraint("CK_manufacturing_machine_calibration_dates", "\"NextDueAt\" > \"CalibratedAt\"");
                table.ForeignKey("FK_manufacturing_machine_calibrations_manufacturing_machines_MachineId", x => x.MachineId, "manufacturing_machines", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_manufacturing_machine_calibrations_TenantKey_MachineId_CertificateNumber", "manufacturing_machine_calibrations", new[] { "TenantKey", "MachineId", "CertificateNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_manufacturing_machine_calibrations_TenantKey_MachineId_NextDueAt", "manufacturing_machine_calibrations", new[] { "TenantKey", "MachineId", "NextDueAt" });
        migrationBuilder.CreateIndex("IX_manufacturing_machine_calibrations_MachineId", "manufacturing_machine_calibrations", "MachineId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "manufacturing_machine_calibrations");
}
