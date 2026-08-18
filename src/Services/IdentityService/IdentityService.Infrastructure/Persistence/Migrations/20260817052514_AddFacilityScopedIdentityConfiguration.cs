using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations;

public partial class AddFacilityScopedIdentityConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("fk_localization_translations_localization_resources_resource_k", "localization_translations");
        migrationBuilder.DropPrimaryKey("pk_system_settings", "system_settings");
        migrationBuilder.DropPrimaryKey("pk_localization_translations", "localization_translations");
        migrationBuilder.DropPrimaryKey("pk_localization_resources", "localization_resources");
        migrationBuilder.DropIndex("ix_device_posture_assessments_provider_evidence_hash", "device_posture_assessments");

        migrationBuilder.AddColumn<string>("scope_id", "system_settings", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>("scope_id", "localization_resources", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>("scope_id", "localization_translations", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>("scope_id", "device_posture_policies", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>("scope_id", "device_posture_assessments", maxLength: 100, nullable: true);

        migrationBuilder.Sql("UPDATE system_settings SET scope_id = 'global' WHERE scope_id IS NULL;");
        migrationBuilder.Sql("UPDATE localization_resources SET scope_id = 'global' WHERE scope_id IS NULL;");
        migrationBuilder.Sql("UPDATE localization_translations SET scope_id = 'global' WHERE scope_id IS NULL;");
        migrationBuilder.Sql("UPDATE device_posture_policies SET scope_id = 'global' WHERE scope_id IS NULL;");
        migrationBuilder.Sql("UPDATE device_posture_assessments SET scope_id = 'global' WHERE scope_id IS NULL;");

        foreach (var table in new[] { "system_settings", "localization_resources", "localization_translations", "device_posture_policies", "device_posture_assessments" })
            migrationBuilder.AlterColumn<string>("scope_id", table, maxLength: 100, nullable: false, defaultValue: "global");

        migrationBuilder.AddPrimaryKey("pk_system_settings", "system_settings", new[] { "scope_id", "key" });
        migrationBuilder.AddPrimaryKey("pk_localization_resources", "localization_resources", new[] { "scope_id", "key" });
        migrationBuilder.AddPrimaryKey("pk_localization_translations", "localization_translations", new[] { "scope_id", "resource_key", "locale" });
        migrationBuilder.AddForeignKey(
            name: "fk_localization_translations_localization_resources_scope_id_resource_key",
            table: "localization_translations",
            columns: new[] { "scope_id", "resource_key" },
            principalTable: "localization_resources",
            principalColumns: new[] { "scope_id", "key" },
            onDelete: ReferentialAction.Cascade);
        migrationBuilder.CreateIndex("ix_device_posture_assessments_scope_id_provider_evidence_hash", "device_posture_assessments", new[] { "scope_id", "provider", "evidence_hash" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("fk_localization_translations_localization_resources_scope_id_resource_key", "localization_translations");
        migrationBuilder.DropPrimaryKey("pk_system_settings", "system_settings");
        migrationBuilder.DropPrimaryKey("pk_localization_resources", "localization_resources");
        migrationBuilder.DropPrimaryKey("pk_localization_translations", "localization_translations");
        migrationBuilder.DropIndex("ix_device_posture_assessments_scope_id_provider_evidence_hash", "device_posture_assessments");
        migrationBuilder.AddPrimaryKey("pk_system_settings", "system_settings", "key");
        migrationBuilder.AddPrimaryKey("pk_localization_resources", "localization_resources", "key");
        migrationBuilder.AddPrimaryKey("pk_localization_translations", "localization_translations", new[] { "resource_key", "locale" });
        migrationBuilder.AddForeignKey("fk_localization_translations_localization_resources_resource_k", "localization_translations", "resource_key", "localization_resources", "key", onDelete: ReferentialAction.Cascade);
        migrationBuilder.CreateIndex("ix_device_posture_assessments_provider_evidence_hash", "device_posture_assessments", new[] { "provider", "evidence_hash" }, unique: true);
        migrationBuilder.DropColumn("scope_id", "system_settings");
        migrationBuilder.DropColumn("scope_id", "localization_resources");
        migrationBuilder.DropColumn("scope_id", "localization_translations");
        migrationBuilder.DropColumn("scope_id", "device_posture_policies");
        migrationBuilder.DropColumn("scope_id", "device_posture_assessments");
    }
}
