using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCapaSupplierEvaluationsAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturing_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_capas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeviationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ProblemDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RootCause = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CorrectiveAction = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PreventiveAction = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_capas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manufacturing_capas_manufacturing_deviations_DeviationId",
                        column: x => x.DeviationId,
                        principalTable: "manufacturing_deviations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_manufacturing_capas_manufacturing_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "manufacturing_suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "manufacturing_supplier_evaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    QualityNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DeliveryNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EvaluatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturing_supplier_evaluations", x => x.Id);
                    table.CheckConstraint("CK_manufacturing_supplier_evaluations_score", "\"Score\" >= 1 AND \"Score\" <= 5");
                    table.ForeignKey(
                        name: "FK_manufacturing_supplier_evaluations_manufacturing_suppliers_~",
                        column: x => x.SupplierId,
                        principalTable: "manufacturing_suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_audit_events_TenantKey_EntityType_EntityId_Oc~",
                table: "manufacturing_audit_events",
                columns: new[] { "TenantKey", "EntityType", "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_capas_DeviationId",
                table: "manufacturing_capas",
                column: "DeviationId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_capas_SupplierId",
                table: "manufacturing_capas",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_capas_TenantKey_Status",
                table: "manufacturing_capas",
                columns: new[] { "TenantKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_supplier_evaluations_SupplierId",
                table: "manufacturing_supplier_evaluations",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_manufacturing_supplier_evaluations_TenantKey_SupplierId_Eva~",
                table: "manufacturing_supplier_evaluations",
                columns: new[] { "TenantKey", "SupplierId", "EvaluatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manufacturing_audit_events");

            migrationBuilder.DropTable(
                name: "manufacturing_capas");

            migrationBuilder.DropTable(
                name: "manufacturing_supplier_evaluations");
        }
    }
}
