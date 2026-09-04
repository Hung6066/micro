using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishingOutboxLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "content_publishing_outbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_attempted_at",
                table: "content_publishing_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "content_publishing_outbox",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_until",
                table: "content_publishing_outbox",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "content_publishing_outbox");

            migrationBuilder.DropColumn(
                name: "last_attempted_at",
                table: "content_publishing_outbox");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "content_publishing_outbox");

            migrationBuilder.DropColumn(
                name: "lease_until",
                table: "content_publishing_outbox");
        }
    }
}
