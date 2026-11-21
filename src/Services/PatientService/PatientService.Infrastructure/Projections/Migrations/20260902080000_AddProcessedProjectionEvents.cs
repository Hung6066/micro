using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.PatientService.Infrastructure.Projections.Migrations;

[DbContext(typeof(PatientReadDbContext))]
[Migration("20260902080000_AddProcessedProjectionEvents")]
public partial class AddProcessedProjectionEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "created_by",
            table: "patient_read_models",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "deleted_by",
            table: "patient_read_models",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "deleted_at",
            table: "patient_read_models",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "is_deleted",
            table: "patient_read_models",
            type: "boolean",
            nullable: true,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "updated_by",
            table: "patient_read_models",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "patient_processed_projection_events",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                projection_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
            },
            constraints: table => table.PrimaryKey(
                "pk_patient_processed_projection_events", x => new { x.event_id, x.projection_name }));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "patient_processed_projection_events");
        migrationBuilder.DropColumn(name: "created_by", table: "patient_read_models");
        migrationBuilder.DropColumn(name: "deleted_by", table: "patient_read_models");
        migrationBuilder.DropColumn(name: "deleted_at", table: "patient_read_models");
        migrationBuilder.DropColumn(name: "is_deleted", table: "patient_read_models");
        migrationBuilder.DropColumn(name: "updated_by", table: "patient_read_models");
    }
}
