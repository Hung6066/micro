using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectoryProvisioningOutboxLeaseColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lease_id",
                table: "directory_provisioning_outbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "lease_until",
                table: "directory_provisioning_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_directory_provisioning_outbox_completed_at_lease_until_avai",
                table: "directory_provisioning_outbox",
                columns: new[] { "completed_at", "lease_until", "available_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_directory_provisioning_outbox_completed_at_lease_until_avai",
                table: "directory_provisioning_outbox");

            migrationBuilder.DropColumn(
                name: "lease_id",
                table: "directory_provisioning_outbox");

            migrationBuilder.DropColumn(
                name: "lease_until",
                table: "directory_provisioning_outbox");
        }
    }
}
