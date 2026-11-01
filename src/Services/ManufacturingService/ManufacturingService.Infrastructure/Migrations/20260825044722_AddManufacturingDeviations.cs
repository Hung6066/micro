using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations;

public partial class AddManufacturingDeviations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS manufacturing_deviations (
                "Id" uuid NOT NULL,
                "TenantKey" character varying(100) NOT NULL,
                "ProductionBatchId" uuid NOT NULL,
                "Type" character varying(80) NOT NULL,
                "Description" character varying(2000) NOT NULL,
                "Impact" character varying(2000) NOT NULL,
                "Status" character varying(30) NOT NULL,
                "RequestedBy" character varying(200) NOT NULL,
                "ApprovedBy" character varying(200),
                "ResolutionNotes" character varying(2000),
                "CreatedAt" timestamp with time zone NOT NULL,
                "ApprovedAt" timestamp with time zone,
                "ClosedAt" timestamp with time zone,
                CONSTRAINT "PK_manufacturing_deviations" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_manufacturing_deviations_manufacturing_production_batches_ProductionBatchId"
                    FOREIGN KEY ("ProductionBatchId") REFERENCES manufacturing_production_batches ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_manufacturing_deviations_ProductionBatchId"
                ON manufacturing_deviations ("ProductionBatchId");
            CREATE INDEX IF NOT EXISTS "IX_manufacturing_deviations_TenantKey_ProductionBatchId_Status"
                ON manufacturing_deviations ("TenantKey", "ProductionBatchId", "Status");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("manufacturing_deviations");
}
