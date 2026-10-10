using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace His.Hope.Messaging.Sql.Migrations;

[DbContext(typeof(SqlMessagingDbContext))]
[Migration("20260901000100_InitialSqlMessaging")]
public partial class InitialSqlMessaging : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "his_hope_outbox",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventJson = table.Column<string>(type: "text", nullable: false),
                AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_his_hope_outbox", x => x.Id));

        migrationBuilder.CreateTable(
            name: "his_hope_inbox",
            columns: table => new
            {
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                Consumer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_his_hope_inbox", x => new { x.EventId, x.Consumer }));

        migrationBuilder.CreateTable(
            name: "his_hope_idempotency",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                RequestFingerprint = table.Column<string>(type: "text", nullable: false),
                StatusCode = table.Column<int>(type: "integer", nullable: true),
                Response = table.Column<string>(type: "text", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_his_hope_idempotency", x => x.Key));

        migrationBuilder.CreateIndex(
            name: "IX_his_hope_outbox_PublishedAt_AvailableAt",
            table: "his_hope_outbox",
            columns: new[] { "PublishedAt", "AvailableAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "his_hope_idempotency");
        migrationBuilder.DropTable(name: "his_hope_inbox");
        migrationBuilder.DropTable(name: "his_hope_outbox");
    }
}
