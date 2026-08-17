using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIamBoundariesAndResourcePolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "iam_permission_boundaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allowed_permissions_json = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    resource_constraints_json = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_permission_boundaries", x => x.id);
                    table.ForeignKey(
                        name: "fk_iam_permission_boundaries_iam_scopes_scope_id",
                        column: x => x.scope_id,
                        principalTable: "iam_scopes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "iam_resource_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    resource_pattern = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    statements_json = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    lifecycle_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_resource_policies", x => x.id);
                    table.ForeignKey(
                        name: "fk_iam_resource_policies_iam_scopes_scope_id",
                        column: x => x.scope_id,
                        principalTable: "iam_scopes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_iam_permission_boundaries_principal_id_principal_type_scope",
                table: "iam_permission_boundaries",
                columns: new[] { "principal_id", "principal_type", "scope_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_iam_permission_boundaries_scope_id",
                table: "iam_permission_boundaries",
                column: "scope_id");

            migrationBuilder.CreateIndex(
                name: "ix_iam_resource_policies_scope_id_service_key_resource_pattern",
                table: "iam_resource_policies",
                columns: new[] { "scope_id", "service_key", "resource_pattern" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "iam_permission_boundaries");

            migrationBuilder.DropTable(
                name: "iam_resource_policies");
        }
    }
}
