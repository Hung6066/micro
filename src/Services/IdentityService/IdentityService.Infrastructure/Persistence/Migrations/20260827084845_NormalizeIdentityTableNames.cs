using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeIdentityTableNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE permissions RENAME TO iam_permissions;");
            migrationBuilder.Sql("ALTER TABLE openiddict_consents RENAME TO client_consents;");
            migrationBuilder.Sql("ALTER TABLE admin_table_views RENAME TO user_table_views;");
            migrationBuilder.Sql("ALTER INDEX pk_permissions RENAME TO pk_iam_permissions;");
            migrationBuilder.Sql("ALTER INDEX ix_permissions_group RENAME TO ix_iam_permissions_group;");
            migrationBuilder.Sql("ALTER INDEX pk_openiddict_consents RENAME TO pk_client_consents;");
            migrationBuilder.Sql("ALTER INDEX ix_openiddict_consents_client_id RENAME TO ix_client_consents_client_id;");
            migrationBuilder.Sql("ALTER INDEX ix_openiddict_consents_user_id RENAME TO ix_client_consents_user_id;");
            migrationBuilder.Sql("ALTER INDEX ix_openiddict_consents_user_id_client_id RENAME TO ix_client_consents_user_id_client_id;");
            migrationBuilder.Sql("ALTER INDEX pk_admin_table_views RENAME TO pk_user_table_views;");
            migrationBuilder.Sql("ALTER INDEX ix_admin_table_views_user_id_resource_name RENAME TO ix_user_table_views_user_id_resource_name;");

            // Keep the legacy relation names as updatable compatibility views
            // during rolling deployments. They can be removed after all pods
            // use the canonical names.
            migrationBuilder.Sql("CREATE OR REPLACE VIEW permissions AS SELECT * FROM iam_permissions;");
            migrationBuilder.Sql("CREATE OR REPLACE VIEW openiddict_consents AS SELECT * FROM client_consents;");
            migrationBuilder.Sql("CREATE OR REPLACE VIEW admin_table_views AS SELECT * FROM user_table_views;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS permissions;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS openiddict_consents;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS admin_table_views;");

            migrationBuilder.Sql("ALTER TABLE user_table_views RENAME TO admin_table_views;");
            migrationBuilder.Sql("ALTER TABLE iam_permissions RENAME TO permissions;");
            migrationBuilder.Sql("ALTER TABLE client_consents RENAME TO openiddict_consents;");
            migrationBuilder.Sql("ALTER INDEX pk_user_table_views RENAME TO pk_admin_table_views;");
            migrationBuilder.Sql("ALTER INDEX ix_user_table_views_user_id_resource_name RENAME TO ix_admin_table_views_user_id_resource_name;");
            migrationBuilder.Sql("ALTER INDEX pk_iam_permissions RENAME TO pk_permissions;");
            migrationBuilder.Sql("ALTER INDEX ix_iam_permissions_group RENAME TO ix_permissions_group;");
            migrationBuilder.Sql("ALTER INDEX pk_client_consents RENAME TO pk_openiddict_consents;");
            migrationBuilder.Sql("ALTER INDEX ix_client_consents_client_id RENAME TO ix_openiddict_consents_client_id;");
            migrationBuilder.Sql("ALTER INDEX ix_client_consents_user_id RENAME TO ix_openiddict_consents_user_id;");
            migrationBuilder.Sql("ALTER INDEX ix_client_consents_user_id_client_id RENAME TO ix_openiddict_consents_user_id_client_id;");
        }
    }
}
