using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.Messaging.Sql.Migrations;

[DbContext(typeof(SqlMessagingDbContext))]
[Migration("20260901170000_AddInboxProcessingLease")]
public partial class AddInboxProcessingLease : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ProcessingAt",
            table: "his_hope_inbox",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE \"his_hope_inbox\" SET \"ProcessingAt\" = COALESCE(\"CompletedAt\", CURRENT_TIMESTAMP) WHERE \"ProcessingAt\" IS NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ProcessingAt",
            table: "his_hope_inbox");
    }
}
