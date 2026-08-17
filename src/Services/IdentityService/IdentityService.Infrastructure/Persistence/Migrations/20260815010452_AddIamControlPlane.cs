using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIamControlPlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "iam_scopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_scopes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "iam_service_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    permission_prefix = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    owner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_service_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "iam_permission_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permissions_json = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    lifecycle_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_permission_sets", x => x.id);
                    table.ForeignKey(
                        name: "fk_iam_permission_sets_iam_scopes_scope_id",
                        column: x => x.scope_id,
                        principalTable: "iam_scopes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "iam_permission_set_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_permission_set_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_iam_permission_set_assignments_iam_permission_sets_permissi",
                        column: x => x.permission_set_id,
                        principalTable: "iam_permission_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_iam_permission_set_assignments_iam_scopes_scope_id",
                        column: x => x.scope_id,
                        principalTable: "iam_scopes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_iam_permission_set_assignments_permission_set_id_principal_",
                table: "iam_permission_set_assignments",
                columns: new[] { "permission_set_id", "principal_id", "scope_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_iam_permission_set_assignments_principal_id_scope_id_status",
                table: "iam_permission_set_assignments",
                columns: new[] { "principal_id", "scope_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_iam_permission_set_assignments_scope_id",
                table: "iam_permission_set_assignments",
                column: "scope_id");

            migrationBuilder.CreateIndex(
                name: "ix_iam_permission_sets_scope_id_key",
                table: "iam_permission_sets",
                columns: new[] { "scope_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_iam_scopes_kind_key",
                table: "iam_scopes",
                columns: new[] { "kind", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_iam_scopes_parent_id",
                table: "iam_scopes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_iam_service_definitions_key",
                table: "iam_service_definitions",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "iam_permission_set_assignments");

            migrationBuilder.DropTable(
                name: "iam_service_definitions");

            migrationBuilder.DropTable(
                name: "iam_permission_sets");

            migrationBuilder.DropTable(
                name: "iam_scopes");
        }
    }
}
