using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.PharmacyService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Medications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    genericname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    brandname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    dosageform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    strength = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    route = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requiresprescription = table.Column<bool>(type: "boolean", nullable: false),
                    isactive = table.Column<bool>(type: "boolean", nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medications", x => x.id);
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
                name: "Prescriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patientid = table.Column<Guid>(type: "uuid", nullable: false),
                    providerid = table.Column<Guid>(type: "uuid", nullable: false),
                    medicationid = table.Column<Guid>(type: "uuid", nullable: true),
                    medicationname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    strength = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dosageform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dosageinstructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    route = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    refills = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prescribeddate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expirydate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    filleddate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelleddate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellationreason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescriptions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Medications_genericname",
                table: "Medications",
                column: "genericname");

            migrationBuilder.CreateIndex(
                name: "IX_Medications_isactive",
                table: "Medications",
                column: "isactive");

            migrationBuilder.CreateIndex(
                name: "IX_Medications_name",
                table: "Medications",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_status_occurredon",
                table: "OutboxMessages",
                columns: new[] { "status", "occurredon" });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_patientid",
                table: "Prescriptions",
                column: "patientid");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_prescribeddate",
                table: "Prescriptions",
                column: "prescribeddate");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_providerid",
                table: "Prescriptions",
                column: "providerid");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_status",
                table: "Prescriptions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Medications");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "Prescriptions");
        }
    }
}
