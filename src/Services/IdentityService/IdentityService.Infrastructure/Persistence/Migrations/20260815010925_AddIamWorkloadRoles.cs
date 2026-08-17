using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIamWorkloadRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "iam_workload_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    audience = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    trust_policy_json = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    permissions_json = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    max_session_seconds = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iam_workload_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_iam_workload_roles_iam_scopes_scope_id",
                        column: x => x.scope_id,
                        principalTable: "iam_scopes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_iam_workload_roles_scope_id_key",
                table: "iam_workload_roles",
                columns: new[] { "scope_id", "key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "iam_workload_roles");
        }
    }
}
