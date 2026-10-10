using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ManufacturingService.Api.Migrations;

[Migration("20260827081000_StandardizePhysicalIdentifiers")]
[DbContext(typeof(ManufacturingDbContext))]
public partial class StandardizePhysicalIdentifiers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$
        DECLARE item record; new_name text; collision boolean;
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
                EXECUTE format('SELECT EXISTS (SELECT 1 FROM %I.%I WHERE %I IS NOT NULL AND %I IS NOT NULL)', item.table_schema, item.table_name, new_name, item.column_name) INTO collision;
                IF collision THEN
                  RAISE EXCEPTION 'Cannot merge physical columns %.% and %.% because both contain data', item.table_name, item.column_name, item.table_name, new_name;
                END IF;
                EXECUTE format('UPDATE %I.%I SET %I = %I WHERE %I IS NULL', item.table_schema, item.table_name, new_name, item.column_name, new_name);
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
