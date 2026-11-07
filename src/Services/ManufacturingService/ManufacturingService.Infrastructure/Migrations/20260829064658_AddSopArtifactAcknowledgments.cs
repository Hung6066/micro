using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSopArtifactAcknowledgments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_sop_artifact_acknowledgments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sop_artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_sop_artifact_acknowledgments", x => x.id);
                    table.ForeignKey(
                        name: "FK_manufacturing_sop_artifact_acknowledgments_manufacturing_so~",
                        column: x => x.sop_artifact_id,
                        principalTable: "manufacturing_sop_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_sop_artifact_acknowledgments_sop_artifact_id",
                table: "manufacturing_sop_artifact_acknowledgments",
                column: "sop_artifact_id");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_sop_artifact_acknowledgments_tenant_key_sop_a~",
                table: "manufacturing_sop_artifact_acknowledgments",
                columns: new[] { "tenant_key", "sop_artifact_id", "actor" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_sop_artifact_acknowledgments");
        }
    }
}
