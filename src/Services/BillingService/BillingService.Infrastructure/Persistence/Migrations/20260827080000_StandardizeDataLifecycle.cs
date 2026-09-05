using Microsoft.EntityFrameworkCore.Migrations;

namespace His.Hope.BillingService.Infrastructure.Persistence.Migrations;

[Migration("20260827080000_StandardizeDataLifecycle")]
public partial class StandardizeDataLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(LifecycleSql);
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("The lifecycle contract is additive and is not reverted automatically.");
    private const string LifecycleSql = """
        DO $$ DECLARE item record; column_item record; normalized_name text; BEGIN
          FOR item IN SELECT table_schema, table_name FROM information_schema.tables
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema') AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory' LOOP
            normalized_name := lower(regexp_replace(regexp_replace(item.table_name, '([a-z0-9])([A-Z])', '\1_\2', 'g'), '([A-Z]+)([A-Z][a-z])', '\1_\2', 'g'));
            IF normalized_name <> item.table_name AND NOT EXISTS (
              SELECT 1 FROM information_schema.tables
              WHERE table_schema = item.table_schema AND table_name = normalized_name
            ) THEN
              EXECUTE format('ALTER TABLE %I.%I RENAME TO %I', item.table_schema, item.table_name, normalized_name);
            END IF;
          END LOOP;
          FOR column_item IN SELECT table_schema, table_name, column_name FROM information_schema.columns
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema') AND table_name <> '__EFMigrationsHistory' LOOP
            normalized_name := lower(regexp_replace(regexp_replace(column_item.column_name, '([a-z0-9])([A-Z])', '\1_\2', 'g'), '([A-Z]+)([A-Z][a-z])', '\1_\2', 'g'));
            IF normalized_name <> column_item.column_name AND NOT EXISTS (
              SELECT 1 FROM information_schema.columns
              WHERE table_schema = column_item.table_schema AND table_name = column_item.table_name AND column_name = normalized_name
            ) THEN
              EXECUTE format('ALTER TABLE %I.%I RENAME COLUMN %I TO %I', column_item.table_schema, column_item.table_name, column_item.column_name, normalized_name);
            END IF;
          END LOOP;
          FOR item IN SELECT table_schema, table_name FROM information_schema.tables
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema') AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory' LOOP
            EXECUTE format('ALTER TABLE %I.%I ADD COLUMN IF NOT EXISTS created_at timestamptz', item.table_schema, item.table_name);
            EXECUTE format('ALTER TABLE %I.%I ADD COLUMN IF NOT EXISTS created_by varchar(256)', item.table_schema, item.table_name);
            EXECUTE format('ALTER TABLE %I.%I ADD COLUMN IF NOT EXISTS updated_at timestamptz', item.table_schema, item.table_name);
            EXECUTE format('ALTER TABLE %I.%I ADD COLUMN IF NOT EXISTS updated_by varchar(256)', item.table_schema, item.table_name);
            EXECUTE format('ALTER TABLE %I.%I ADD COLUMN IF NOT EXISTS is_deleted boolean DEFAULT false', item.table_schema, item.table_name);
            EXECUTE format('ALTER TABLE %I.%I ADD COLUMN IF NOT EXISTS deleted_at timestamptz', item.table_schema, item.table_name);
            EXECUTE format('ALTER TABLE %I.%I ADD COLUMN IF NOT EXISTS deleted_by varchar(256)', item.table_schema, item.table_name);
          END LOOP;
        END $$;
        """;
}
