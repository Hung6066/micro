using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "authorization_version",
                table: "asp_net_roles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "lifecycle_status",
                table: "asp_net_roles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "active");

            migrationBuilder.AddColumn<string>(
                name: "owner",
                table: "asp_net_roles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "identity-service");

            migrationBuilder.AddColumn<DateTime>(
                name: "published_at",
                table: "asp_net_roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "published_by",
                table: "asp_net_roles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "review_cadence_days",
                table: "asp_net_roles",
                type: "integer",
                nullable: false,
                defaultValue: 180);

            migrationBuilder.AddColumn<string>(
                name: "risk_tier",
                table: "asp_net_roles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "standard");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "authorization_version",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "lifecycle_status",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "owner",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "published_at",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "published_by",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "review_cadence_days",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "risk_tier",
                table: "asp_net_roles");
        }
    }
}
