using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace His.Hope.ClinicalService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clinical_notes",
                columns: table => new
                {
                    noteid = table.Column<Guid>(type: "uuid", nullable: false),
                    encounterid = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    notetype = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createdby = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinical_notes", x => x.noteid);
                });

            migrationBuilder.CreateTable(
                name: "encounters",
                columns: table => new
                {
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EncounterDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EncounterType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChiefComplaint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    hpi_onset = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    hpi_location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    hpi_duration = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    hpi_characteristics = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    hpi_aggravating_factors = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    hpi_relieving_factors = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    hpi_prior_treatments = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    temperature = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    heart_rate = table.Column<int>(type: "integer", nullable: true),
                    respiratory_rate = table.Column<int>(type: "integer", nullable: true),
                    systolic_bp = table.Column<int>(type: "integer", nullable: true),
                    diastolic_bp = table.Column<int>(type: "integer", nullable: true),
                    oxygen_saturation = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    height_cm = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    bmi = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Assessment = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Plan = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    DiagnosisNotes = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounters", x => x.encounter_id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CausationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastRetryOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadLetteredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "encounter_diagnoses",
                columns: table => new
                {
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    condition_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    icd10_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounter_diagnoses", x => new { x.encounter_id, x.Id });
                    table.ForeignKey(
                        name: "FK_encounter_diagnoses_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalTable: "encounters",
                        principalColumn: "encounter_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "encounter_procedures",
                columns: table => new
                {
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    procedure_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    cpt_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    performed_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounter_procedures", x => new { x.encounter_id, x.Id });
                    table.ForeignKey(
                        name: "FK_encounter_procedures_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalTable: "encounters",
                        principalColumn: "encounter_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clinical_notes_encounterid",
                table: "clinical_notes",
                column: "encounterid");

            migrationBuilder.CreateIndex(
                name: "ix_encounters_encounterdate",
                table: "encounters",
                column: "EncounterDate");

            migrationBuilder.CreateIndex(
                name: "ix_encounters_patientid",
                table: "encounters",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "ix_encounters_providerid",
                table: "encounters",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_encounters_status",
                table: "encounters",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_outboxmessages_status_occurredon",
                table: "outbox_messages",
                columns: new[] { "Status", "OccurredOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinical_notes");

            migrationBuilder.DropTable(
                name: "encounter_diagnoses");

            migrationBuilder.DropTable(
                name: "encounter_procedures");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "encounters");
        }
    }
}
