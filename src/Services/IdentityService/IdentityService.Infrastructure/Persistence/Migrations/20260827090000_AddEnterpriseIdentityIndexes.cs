using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds the high-cardinality lookup indexes required by the audit and OIDC
/// workloads. The statements are idempotent because the lifecycle migration
/// may already have created them on a fresh database.
/// </summary>
public partial class AddEnterpriseIdentityIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_audit_logs_resource_lookup ON audit_logs (resource_type, resource_id, timestamp);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_audit_logs_user_timeline ON audit_logs (user_id, timestamp);");
        migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ix_openiddict_applications_client_id ON openiddict_applications (client_id);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_openiddict_authorizations_subject_status ON openiddict_authorizations (subject, status);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_openiddict_scopes_name ON openiddict_scopes (name);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_openiddict_tokens_subject_status_expiration ON openiddict_tokens (subject, status, expiration_date);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_openiddict_tokens_status_expiration ON openiddict_tokens (status, expiration_date);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_openiddict_tokens_status_expiration;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_openiddict_tokens_subject_status_expiration;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_openiddict_scopes_name;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_openiddict_authorizations_subject_status;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_openiddict_applications_client_id;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_audit_logs_user_timeline;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_audit_logs_resource_lookup;");
    }
}
