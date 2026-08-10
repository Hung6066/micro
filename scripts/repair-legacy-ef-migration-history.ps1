[CmdletBinding()]
param(
    [string]$PostgresContainer = 'his-hope-postgres',
    [string]$PostgresUser = 'postgres',
    [string]$ProductVersion = '8.0.10'
)

$ErrorActionPreference = 'Stop'

$databases = @(
    @{ Name = 'patientdb'; MigrationId = '20260728145428_InitialCreate'; AllowedMigrationIds = @('20260728145428_InitialCreate', '20260728145504_InitialCreate'); Tables = @('public|outbox_messages', 'public|patients', 'public|allergies', 'public|medical_conditions') },
    @{ Name = 'appointmentdb'; MigrationId = '20260728151050_InitialCreate'; Tables = @('public|appointments', 'public|outbox_messages') },
    @{ Name = 'clinicaldb'; MigrationId = '20260728093306_InitialCreate'; Tables = @('public|clinical_notes', 'public|encounters', 'public|encounter_diagnoses', 'public|encounter_procedures', 'public|outbox_messages') },
    @{ Name = 'labdb'; MigrationId = '20260728145356_InitialCreate'; Tables = @('public|CriticalAlertRules', 'public|CriticalAlerts', 'public|LabOrders', 'public|OutboxMessages', 'public|CriticalAlertAuditEntries', 'public|LabTests', 'public|LabResults') },
    @{ Name = 'billingdb'; MigrationId = '20260728093539_InitialCreate'; Tables = @('billing|Invoices', 'billing|OutboxMessages', 'billing|InvoiceLineItems', 'billing|Payments') },
    @{ Name = 'pharmacydb'; MigrationId = '20260728145334_InitialCreate'; Tables = @('public|Medications', 'public|OutboxMessages', 'public|Prescriptions') }
)

function Invoke-Psql([string]$Database, [string]$Sql) {
    $output = & docker exec $PostgresContainer psql -U $PostgresUser -d $Database -v ON_ERROR_STOP=1 -At -c $Sql
    if ($LASTEXITCODE -ne 0) {
        throw "psql failed for $Database"
    }
    return ($output -join "`n").Trim()
}

foreach ($database in $databases) {
    $expected = ($database.Tables | ForEach-Object {
        $parts = $_.Split('|', 2)
        "('$($parts[0])', '$($parts[1].Replace("'", "''"))')"
    }) -join ', '
    $missing = Invoke-Psql $database.Name @"
SELECT count(*)
FROM (VALUES $expected) AS expected(schema_name, table_name)
LEFT JOIN pg_catalog.pg_tables actual
  ON actual.schemaname = expected.schema_name AND actual.tablename = expected.table_name
WHERE actual.tablename IS NULL;
"@

    if ([int]$missing -ne 0) {
        throw "Refusing to baseline $($database.Name): $missing expected table(s) are missing."
    }

    $allowedMigrationIds = @($database.MigrationId) + @($database.AllowedMigrationIds) | Where-Object { $_ }
    $allowedSql = ($allowedMigrationIds | ForEach-Object { "'$($_.Replace("'", "''"))'" }) -join ', '
    $historyExists = Invoke-Psql $database.Name "SELECT CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory') THEN 1 ELSE 0 END;"
    $historyIdColumn = 'MigrationId'
    $historyProductColumn = 'ProductVersion'
    if ([int]$historyExists -eq 1) {
        $historyIdColumn = Invoke-Psql $database.Name "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory' AND column_name IN ('MigrationId', 'migration_id') ORDER BY CASE column_name WHEN 'MigrationId' THEN 0 ELSE 1 END LIMIT 1;"
        $historyProductColumn = Invoke-Psql $database.Name "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory' AND column_name IN ('ProductVersion', 'product_version') ORDER BY CASE column_name WHEN 'ProductVersion' THEN 0 ELSE 1 END LIMIT 1;"
        if ([string]::IsNullOrWhiteSpace($historyIdColumn) -or [string]::IsNullOrWhiteSpace($historyProductColumn)) {
            throw "Refusing to baseline $($database.Name): migration history table has an unsupported column layout."
        }

        $historyIdColumnSql = '"' + $historyIdColumn + '"'
        $unexpectedHistory = Invoke-Psql $database.Name @"
SELECT count(*)
FROM "__EFMigrationsHistory"
WHERE $historyIdColumnSql NOT IN ($allowedSql);
"@
        if ([int]$unexpectedHistory -gt 0) {
            throw "Refusing to baseline $($database.Name): migration history contains $unexpectedHistory unexpected row(s)."
        }

        $alreadyBaselined = Invoke-Psql $database.Name @"
SELECT count(*)
FROM "__EFMigrationsHistory"
WHERE $historyIdColumnSql = '$($database.MigrationId)';
"@
        if ([int]$alreadyBaselined -eq 1) {
            Write-Output "Already baselined $($database.Name) -> $($database.MigrationId)"
            continue
        }
    }

    $migrationId = $database.MigrationId.Replace("'", "''")
    $historyIdColumnSql = '"' + $historyIdColumn + '"'
    $historyProductColumnSql = '"' + $historyProductColumn + '"'
    $createHistory = if ([int]$historyExists -eq 0) {
        @"
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory"
(
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);
"@
    } else { '' }
    $sql = @"
BEGIN;
$createHistory
INSERT INTO "__EFMigrationsHistory" ($historyIdColumnSql, $historyProductColumnSql)
VALUES ('$migrationId', '$ProductVersion')
ON CONFLICT DO NOTHING;
COMMIT;
"@
    Invoke-Psql $database.Name $sql | Out-Null
    Write-Output "Baselined $($database.Name) -> $($database.MigrationId)"
}
