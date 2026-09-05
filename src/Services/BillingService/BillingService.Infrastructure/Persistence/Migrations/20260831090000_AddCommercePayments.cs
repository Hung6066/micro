using Microsoft.EntityFrameworkCore.Migrations;

namespace BillingService.Infrastructure.Persistence.Migrations;

[Migration("20260831090000_AddCommercePayments")]
public partial class AddCommercePayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CommercePayments",
            schema: "billing",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Amount = table.Column<decimal>(type: "numeric", nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ProviderPaymentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                FailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CommercePayments", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_CommercePayments_TenantKey_OrderId", table: "CommercePayments", columns: new[] { "TenantKey", "OrderId" }, schema: "billing", unique: true);
        migrationBuilder.CreateIndex(name: "IX_CommercePayments_TenantKey_IdempotencyKey", table: "CommercePayments", columns: new[] { "TenantKey", "IdempotencyKey" }, schema: "billing", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "CommercePayments", schema: "billing");
}
