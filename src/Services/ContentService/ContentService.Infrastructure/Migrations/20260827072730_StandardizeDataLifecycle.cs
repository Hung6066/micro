using Microsoft.EntityFrameworkCore.Migrations;

namespace His.Hope.ContentService.Infrastructure.Migrations;

[Migration("20260827072730_StandardizeDataLifecycle")]
public partial class StandardizeDataLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        DECLARE item record; new_name text;
        BEGIN
          FOR item IN
            SELECT table_schema, table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND column_name ~ '[A-Z]'
              AND table_name <> '__EFMigrationsHistory'
          LOOP
            new_name := lower(regexp_replace(regexp_replace(item.column_name, '([A-Z]+)([A-Z][a-z])', '\1_\2', 'g'), '([a-z0-9])([A-Z])', '\1_\2', 'g'));
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
