-- ============================================================================
-- Fix column casing: lowercase -> PascalCase for EF Core compatibility
-- Converts: patientid, first_name, isactive -> PatientId, FirstName, IsActive
-- ============================================================================

CREATE OR REPLACE FUNCTION fix_column_casing() RETURNS integer AS $$
DECLARE
    rec RECORD;
    new_name TEXT;
    col_count INT := 0;
BEGIN
    FOR rec IN
        SELECT c.table_name::text, c.column_name::text
        FROM information_schema.columns c
        JOIN information_schema.tables t ON c.table_name = t.table_name AND c.table_schema = t.table_schema
        WHERE c.table_schema = 'public'
          AND t.table_type = 'BASE TABLE'
          AND c.column_name ~ '^[a-z]'
        ORDER BY c.table_name, c.ordinal_position
    LOOP
        -- Convert snake_case to PascalCase
        new_name := initcap(replace(rec.column_name, '_', ' '));
        new_name := replace(new_name, ' ', '');
        new_name := upper(substr(new_name, 1, 1)) || substr(new_name, 2);

        IF new_name != rec.column_name THEN
            BEGIN
                EXECUTE format('ALTER TABLE %I RENAME COLUMN %I TO %I',
                    rec.table_name, rec.column_name, new_name);
                col_count := col_count + 1;
                RAISE NOTICE 'Renamed %.% -> %', rec.table_name, rec.column_name, new_name;
            EXCEPTION WHEN OTHERS THEN
                RAISE NOTICE 'FAILED: %.% -> % : %', rec.table_name, rec.column_name, new_name, SQLERRM;
            END;
        END IF;
    END LOOP;
    RETURN col_count;
END;
$$ LANGUAGE plpgsql;

SELECT fix_column_casing();
DROP FUNCTION fix_column_casing();
