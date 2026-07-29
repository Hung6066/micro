using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.LabService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CriticalAlertRules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    testcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    testname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    lowcriticalvalue = table.Column<decimal>(type: "numeric", nullable: true),
                    highcriticalvalue = table.Column<decimal>(type: "numeric", nullable: true),
                    isactive = table.Column<bool>(type: "boolean", nullable: false),
                    createdbyuserid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    createdbydisplayname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriticalAlertRules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "CriticalAlerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    laborderid = table.Column<Guid>(type: "uuid", nullable: false),
                    labtestid = table.Column<Guid>(type: "uuid", nullable: false),
                    labresultid = table.Column<Guid>(type: "uuid", nullable: false),
                    ruleid = table.Column<Guid>(type: "uuid", nullable: true),
                    triggertype = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    resultvalue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    resultunit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    thresholdvalue = table.Column<decimal>(type: "numeric", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    acknowledgedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    acknowledgedbyuserid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    acknowledgedbydisplayname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    resolvedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolvedbyuserid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resolvedbydisplayname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriticalAlerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "LabOrders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patientid = table.Column<Guid>(type: "uuid", nullable: false),
                    providerid = table.Column<Guid>(type: "uuid", nullable: false),
                    encounterid = table.Column<Guid>(type: "uuid", nullable: true),
                    orderdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    correlationid = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    causationid = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    occurredon = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processedon = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    retrycount = table.Column<int>(type: "integer", nullable: false),
                    lastretryon = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lockexpiresat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadLetteredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "CriticalAlertAuditEntries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    criticalalertid = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actoruserid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actordisplayname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    occurredat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriticalAlertAuditEntries", x => x.id);
                    table.ForeignKey(
                        name: "FK_CriticalAlertAuditEntries_CriticalAlerts_criticalalertid",
                        column: x => x.criticalalertid,
                        principalTable: "CriticalAlerts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabTests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    laborderid = table.Column<Guid>(type: "uuid", nullable: false),
                    testcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    testname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    specimentype = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    orderedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    collectedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTests", x => x.id);
                    table.ForeignKey(
                        name: "FK_LabTests_LabOrders_laborderid",
                        column: x => x.laborderid,
                        principalTable: "LabOrders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabResults",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    labresultid = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    referencerange = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    abnormalflag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    resultstatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resultedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    performedby = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    labtestid = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResults", x => x.id);
                    table.ForeignKey(
                        name: "FK_LabResults_LabTests_labtestid",
                        column: x => x.labtestid,
                        principalTable: "LabTests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalAlertAuditEntries_criticalalertid",
                table: "CriticalAlertAuditEntries",
                column: "criticalalertid");

            migrationBuilder.CreateIndex(
                name: "IX_CriticalAlertAuditEntries_occurredat",
                table: "CriticalAlertAuditEntries",
                column: "occurredat");

            migrationBuilder.CreateIndex(
                name: "IX_CriticalAlertRules_testcode",
                table: "CriticalAlertRules",
                column: "testcode");

            migrationBuilder.CreateIndex(
                name: "IX_CriticalAlertRules_testcode_isactive",
                table: "CriticalAlertRules",
                columns: new[] { "testcode", "isactive" });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalAlerts_laborderid_labtestid",
                table: "CriticalAlerts",
                columns: new[] { "laborderid", "labtestid" },
                unique: true,
                filter: "status <> 'RESOLVED'");

            migrationBuilder.CreateIndex(
                name: "IX_CriticalAlerts_status",
                table: "CriticalAlerts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_orderdate",
                table: "LabOrders",
                column: "orderdate");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_patientid",
                table: "LabOrders",
                column: "patientid");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_providerid",
                table: "LabOrders",
                column: "providerid");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_status",
                table: "LabOrders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_labtestid",
                table: "LabResults",
                column: "labtestid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_laborderid",
                table: "LabTests",
                column: "laborderid");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_status_occurredon",
                table: "OutboxMessages",
                columns: new[] { "status", "occurredon" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CriticalAlertAuditEntries");

            migrationBuilder.DropTable(
                name: "CriticalAlertRules");

            migrationBuilder.DropTable(
                name: "LabResults");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "CriticalAlerts");

            migrationBuilder.DropTable(
                name: "LabTests");

            migrationBuilder.DropTable(
                name: "LabOrders");
        }
    }
}
