using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LinkRecipesToProductSpecifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductSpecificationId",
                table: "manufacturing_recipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_recipes_ProductSpecificationId",
                table: "manufacturing_recipes",
                column: "ProductSpecificationId");

            migrationBuilder.AddForeignKey(
                name: "FK_manufacturing_recipes_manufacturing_product_specifications_~",
                table: "manufacturing_recipes",
                column: "ProductSpecificationId",
                principalTable: "manufacturing_product_specifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_manufacturing_recipes_manufacturing_product_specifications_~",
                table: "manufacturing_recipes");

            migrationBuilder.DropIndex(
                name: "IX_manufacturing_recipes_ProductSpecificationId",
                table: "manufacturing_recipes");

            migrationBuilder.DropColumn(
                name: "ProductSpecificationId",
                table: "manufacturing_recipes");
        }
    }
}
