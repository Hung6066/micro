using Microsoft.EntityFrameworkCore.Migrations;

namespace His.Hope.LabService.Infrastructure.Persistence.Migrations;

[Migration("20260827080000_StandardizeDataLifecycle")]
public partial class StandardizeDataLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(LifecycleSql);
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("The lifecycle contract is additive and is not reverted automatically.");
    private const string LifecycleSql = """
        DO $$ DECLARE item record; BEGIN
          FOR item IN SELECT table_schema, table_name FROM information_schema.tables
            WHERE table_schema = current_schema() AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory' LOOP
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
