using Microsoft.EntityFrameworkCore.Migrations;

namespace His.Hope.LabService.Infrastructure.Persistence.Migrations;

[Migration("20260827082000_StandardizePhysicalIdentifiers")]
public partial class StandardizePhysicalIdentifiers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        DECLARE item record; new_name text;
        BEGIN
          FOR item IN
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema = current_schema()
              AND table_type = 'BASE TABLE'
              AND table_name ~ '[A-Z]'
              AND table_name <> '__EFMigrationsHistory'
          LOOP
            new_name := lower(regexp_replace(regexp_replace(item.table_name, '([A-Z]+)([A-Z][a-z])', '\1_\2', 'g'), '([a-z0-9])([A-Z])', '\1_\2', 'g'));
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables t WHERE t.table_schema = item.table_schema AND t.table_name = new_name) THEN
              EXECUTE format('ALTER TABLE %I.%I RENAME TO %I', item.table_schema, item.table_name, new_name);
            END IF;
          END LOOP;
          FOR item IN
            SELECT table_schema, table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND column_name ~ '[A-Z]'
              AND table_name <> '__EFMigrationsHistory'
          LOOP
            new_name := lower(regexp_replace(regexp_replace(item.column_name, '([A-Z]+)([A-Z][a-z])', '\\1_\\2', 'g'), '([a-z0-9])([A-Z])', '\\1_\\2', 'g'));
            IF new_name <> item.column_name THEN
              IF EXISTS (SELECT 1 FROM information_schema.columns c WHERE c.table_schema = item.table_schema AND c.table_name = item.table_name AND c.column_name = new_name) THEN
                EXECUTE format('UPDATE %I.%I SET %I = COALESCE(%I, %I)', item.table_schema, item.table_name, new_name, new_name, item.column_name);
                EXECUTE format('ALTER TABLE %I.%I DROP COLUMN %I', item.table_schema, item.table_name, item.column_name);
              ELSE
                EXECUTE format('ALTER TABLE %I.%I RENAME COLUMN %I TO %I', item.table_schema, item.table_name, item.column_name, new_name);
              END IF;
            END IF;
          END LOOP;
        END $$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Physical identifier renames are not reversed automatically.");
}
