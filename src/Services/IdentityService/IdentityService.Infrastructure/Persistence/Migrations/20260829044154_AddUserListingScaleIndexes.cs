using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserListingScaleIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_created_at_id",
                table: "asp_net_users",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_active_created_at_id",
                table: "asp_net_users",
                columns: new[] { "is_active", "created_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_asp_net_users_active_created_at_id", table: "asp_net_users");
            migrationBuilder.DropIndex(name: "ix_asp_net_users_created_at_id", table: "asp_net_users");
        }
    }
}
