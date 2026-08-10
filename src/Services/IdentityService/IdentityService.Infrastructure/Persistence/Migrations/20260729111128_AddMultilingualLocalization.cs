using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultilingualLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "preferred_language",
                table: "asp_net_users",
                type: "character varying(35)",
                maxLength: 35,
                nullable: false,
                defaultValue: "vi-VN");

            migrationBuilder.CreateTable(
                name: "localization_resources",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_localization_resources", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "localization_translations",
                columns: table => new
                {
                    resource_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    locale = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_localization_translations", x => new { x.resource_key, x.locale });
                    table.ForeignKey(
                        name: "fk_localization_translations_localization_resources_resource_k",
                        column: x => x.resource_key,
                        principalTable: "localization_resources",
                        principalColumn: "key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_localization_translations_locale",
                table: "localization_translations",
                column: "locale");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "localization_translations");

            migrationBuilder.DropTable(
                name: "localization_resources");

            migrationBuilder.DropColumn(
                name: "preferred_language",
                table: "asp_net_users");
        }
    }
}
