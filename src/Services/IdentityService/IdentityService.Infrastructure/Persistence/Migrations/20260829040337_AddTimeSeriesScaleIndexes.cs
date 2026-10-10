using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeSeriesScaleIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add compact indexes for time-window scans without replacing the
            // existing B-tree indexes used by point lookups.
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_security_events_timestamp_brin ON security_events USING BRIN (timestamp);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_audit_logs_timestamp_brin ON audit_logs USING BRIN (timestamp);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_security_events_timestamp_brin;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_audit_logs_timestamp_brin;");
        }
    }
}
