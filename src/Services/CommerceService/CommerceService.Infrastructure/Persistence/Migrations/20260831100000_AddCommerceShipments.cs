using Microsoft.EntityFrameworkCore.Migrations;

namespace CommerceService.Infrastructure.Persistence.Migrations;

[Migration("20260831100000_AddCommerceShipments")]
public partial class AddCommerceShipments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "commerce_shipments", schema: "commerce",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ProviderShipmentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_commerce_shipments", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_commerce_shipments_TenantKey_OrderId", table: "commerce_shipments", columns: new[] { "TenantKey", "OrderId" }, schema: "commerce", unique: true);
        migrationBuilder.CreateIndex(name: "IX_commerce_shipments_TenantKey_IdempotencyKey", table: "commerce_shipments", columns: new[] { "TenantKey", "IdempotencyKey" }, schema: "commerce", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "commerce_shipments", schema: "commerce");
}
