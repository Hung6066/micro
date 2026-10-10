using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ManufacturingService.Infrastructure.Migrations;

[DbContext(typeof(ManufacturingDbContext))]
[Migration("20260828110000_NormalizeDateOnlyColumns")]
public sealed class NormalizeDateOnlyColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // DateOnly properties must remain PostgreSQL `date`. This is defensive
        // for databases created by older snapshots that used timestamp columns.
        migrationBuilder.Sql("""
            DO $$
            DECLARE item record;
            BEGIN
                FOR item IN
                    SELECT * FROM (VALUES
                        ('manufacturing_lots', 'best_before'),
                        ('manufacturing_lots', 'manufactured_on'),
                        ('manufacturing_sales_actuals', 'period_start'),
                        ('manufacturing_sales_actuals', 'period_end'),
                        ('manufacturing_sales_forecasts', 'period_start'),
                        ('manufacturing_sales_forecasts', 'period_end')
                    ) AS columns_to_normalize(table_name, column_name)
                LOOP
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = item.table_name
                          AND column_name = item.column_name
                          AND data_type <> 'date'
                    ) THEN
                        EXECUTE format(
                            'ALTER TABLE %I.%I ALTER COLUMN %I TYPE date USING %I::date',
                            current_schema(), item.table_name, item.column_name, item.column_name);
                    END IF;
                END LOOP;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("DateOnly columns are intentionally normalized to PostgreSQL date.");
}
