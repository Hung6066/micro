using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_supplier_certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_supplier_certificates", x => x.Id);
                    table.CheckConstraint("CK_manufacturing_supplier_certificate_dates", "\"ExpiresAt\" > \"IssuedAt\"");
                    table.ForeignKey(
                        name: "FK_manufacturing_supplier_certificates_manufacturing_suppliers~",
                        column: x => x.SupplierId,
                        principalTable: "manufacturing_suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_supplier_certificates_SupplierId",
                table: "manufacturing_supplier_certificates",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_supplier_certificates_TenantKey_SupplierId_Ce~",
                table: "manufacturing_supplier_certificates",
                columns: new[] { "TenantKey", "SupplierId", "CertificateNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_supplier_certificates");
        }
    }
}
