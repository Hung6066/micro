using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.ClinicalService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_encounters_facility_status_date_id
                    ON encounters (facility_id, "Status", "EncounterDate");
                CREATE INDEX IF NOT EXISTS ix_encounters_patient_date_id
                    ON encounters ("PatientId", "EncounterDate");
                CREATE INDEX IF NOT EXISTS ix_encounters_provider_date_id
                    ON encounters ("ProviderId", "EncounterDate");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_encounters_facility_status_date_id;
                DROP INDEX IF EXISTS ix_encounters_patient_date_id;
                DROP INDEX IF EXISTS ix_encounters_provider_date_id;
                """);
        }
    }
}
