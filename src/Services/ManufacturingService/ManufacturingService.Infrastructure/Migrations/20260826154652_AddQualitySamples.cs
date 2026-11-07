using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations;

public partial class AddQualitySamples : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "manufacturing_quality_samples", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
            LotId = table.Column<Guid>(type: "uuid", nullable: false),
            TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            SampleCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            CollectedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
            CollectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Disposition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
            DispositionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
            DisposedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
            DisposedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
            Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_manufacturing_quality_samples", x => x.Id);
            table.ForeignKey("FK_manufacturing_quality_samples_manufacturing_quality_inspections_InspectionId", x => x.InspectionId, "manufacturing_quality_inspections", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_manufacturing_quality_samples_manufacturing_lots_LotId", x => x.LotId, "manufacturing_lots", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_manufacturing_quality_samples_TenantKey_InspectionId_SampleCode", table: "manufacturing_quality_samples", columns: new[] { "TenantKey", "InspectionId", "SampleCode" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_manufacturing_quality_samples_TenantKey_Disposition_CollectedAt", table: "manufacturing_quality_samples", columns: new[] { "TenantKey", "Disposition", "CollectedAt" });
        migrationBuilder.CreateIndex(name: "IX_manufacturing_quality_samples_InspectionId", table: "manufacturing_quality_samples", column: "InspectionId");
        migrationBuilder.CreateIndex(name: "IX_manufacturing_quality_samples_LotId", table: "manufacturing_quality_samples", column: "LotId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "manufacturing_quality_samples");
}
