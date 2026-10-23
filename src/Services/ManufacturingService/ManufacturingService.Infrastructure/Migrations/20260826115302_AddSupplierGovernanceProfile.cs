using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierGovernanceProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "manufacturing_suppliers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "manufacturing_suppliers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "manufacturing_suppliers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "manufacturing_suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "manufacturing_suppliers",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "manufacturing_suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "manufacturing_suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "manufacturing_suppliers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "manufacturing_suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReviewedAt",
                table: "manufacturing_suppliers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "manufacturing_suppliers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RiskLevel",
                table: "manufacturing_suppliers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxIdentificationNumber",
                table: "manufacturing_suppliers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "manufacturing_suppliers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE manufacturing_suppliers
                SET "LegalName" = COALESCE(NULLIF("LegalName", ''), "Name"),
                    "RiskLevel" = COALESCE(NULLIF("RiskLevel", ''), 'Standard'),
                    "ApprovalStatus" = COALESCE(NULLIF("ApprovalStatus", ''), 'Approved'),
                    "CreatedBy" = COALESCE(NULLIF("CreatedBy", ''), 'migration'),
                    "UpdatedAt" = COALESCE("UpdatedAt", "CreatedAt")
                WHERE "LegalName" = '' OR "RiskLevel" = '' OR "ApprovalStatus" = '' OR "CreatedBy" = '' OR "UpdatedAt" IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_manufacturing_supplier_approval_status",
                table: "manufacturing_suppliers",
                sql: "\"ApprovalStatus\" IN ('Draft', 'PendingApproval', 'Approved', 'Suspended', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_manufacturing_supplier_risk_level",
                table: "manufacturing_suppliers",
                sql: "\"RiskLevel\" IN ('Low', 'Standard', 'High', 'Critical')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_manufacturing_supplier_approval_status",
                table: "manufacturing_suppliers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_manufacturing_supplier_risk_level",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "LastReviewedAt",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "TaxIdentificationNumber",
                table: "manufacturing_suppliers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "manufacturing_suppliers");
        }
    }
}
