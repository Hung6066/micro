using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations;

public partial class AddRecipeLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Status", "manufacturing_recipes", type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Approved");
        migrationBuilder.AddColumn<DateTimeOffset>("EffectiveFrom", "manufacturing_recipes", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("EffectiveTo", "manufacturing_recipes", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>("ApprovedBy", "manufacturing_recipes", type: "character varying(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("ApprovedAt", "manufacturing_recipes", type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateIndex("IX_manufacturing_recipes_TenantKey_ProductSku_Status", "manufacturing_recipes", new[] { "TenantKey", "ProductSku", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_manufacturing_recipes_TenantKey_ProductSku_Status", "manufacturing_recipes");
        migrationBuilder.DropColumn("Status", "manufacturing_recipes");
        migrationBuilder.DropColumn("EffectiveFrom", "manufacturing_recipes");
        migrationBuilder.DropColumn("EffectiveTo", "manufacturing_recipes");
        migrationBuilder.DropColumn("ApprovedBy", "manufacturing_recipes");
        migrationBuilder.DropColumn("ApprovedAt", "manufacturing_recipes");
    }
}
