using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorizationPolicyBundleArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authorization_policy_bundle_artifacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policies_json = table.Column<string>(type: "character varying(120000)", maxLength: 120000, nullable: false),
                    signature = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    key_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authorization_policy_bundle_artifacts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authorization_policy_bundle_artifacts_created_at",
                table: "authorization_policy_bundle_artifacts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_authorization_policy_bundle_artifacts_hash",
                table: "authorization_policy_bundle_artifacts",
                column: "hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authorization_policy_bundle_artifacts");
        }
    }
}
