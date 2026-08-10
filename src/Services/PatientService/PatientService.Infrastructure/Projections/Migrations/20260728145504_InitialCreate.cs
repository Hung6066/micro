using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.PatientService.Infrastructure.Projections.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patient_read_models",
                columns: table => new
                {
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    date_of_birth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    primary_diagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_visit_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    encounter_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patient_read_models", x => x.patient_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_patient_read_models_full_name",
                table: "patient_read_models",
                column: "full_name");

            migrationBuilder.CreateIndex(
                name: "ix_patient_read_models_last_visit_date",
                table: "patient_read_models",
                column: "last_visit_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "patient_read_models");
        }
    }
}
