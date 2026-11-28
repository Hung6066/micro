using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations
{
    /// <inheritdoc />
    public partial class LinkTransformationsToRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecipeId",
                table: "manufacturing_transformations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_transformations_RecipeId",
                table: "manufacturing_transformations",
                column: "RecipeId");

            migrationBuilder.AddForeignKey(
                name: "FK_manufacturing_transformations_manufacturing_recipes_RecipeId",
                table: "manufacturing_transformations",
                column: "RecipeId",
                principalTable: "manufacturing_recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_manufacturing_transformations_manufacturing_recipes_RecipeId",
                table: "manufacturing_transformations");

            migrationBuilder.DropIndex(
                name: "IX_manufacturing_transformations_RecipeId",
                table: "manufacturing_transformations");

            migrationBuilder.DropColumn(
                name: "RecipeId",
                table: "manufacturing_transformations");
        }
    }
}
