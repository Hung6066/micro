using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations;

[DbContext(typeof(ManufacturingDbContext))]
[Migration("20260825024000_AddLossReviews")]
public partial class AddLossReviews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "manufacturing_loss_reviews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ProductionBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                OperationExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                Decision = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Reviewer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_manufacturing_loss_reviews", x => x.Id);
                table.ForeignKey("FK_manufacturing_loss_reviews_manufacturing_operation_executions_OperationExecutionId", x => x.OperationExecutionId, "manufacturing_operation_executions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_manufacturing_loss_reviews_manufacturing_production_batches_ProductionBatchId", x => x.ProductionBatchId, "manufacturing_production_batches", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_manufacturing_loss_reviews_TenantKey_OperationExecutionId", "manufacturing_loss_reviews", new[] { "TenantKey", "OperationExecutionId" }, unique: true);
        migrationBuilder.CreateIndex("IX_manufacturing_loss_reviews_ProductionBatchId", "manufacturing_loss_reviews", "ProductionBatchId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("manufacturing_loss_reviews");
}
