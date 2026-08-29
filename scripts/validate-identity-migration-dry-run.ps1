[CmdletBinding()]
param(
    [string]$MigrationScript = 'artifacts/database-migrations-current/identity-idempotent.sql',
    [string]$HostName = 'localhost',
    [int]$Port = 5433,
    [string]$Username = 'postgres',
    [string]$Password = $env:IDENTITY_MIGRATION_TEST_PASSWORD,
    [string]$OutputPath = 'artifacts/evidence/identity-migration-dry-run.json',
    [switch]$RequireLive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$scriptPath = if ([IO.Path]::IsPathRooted($MigrationScript)) { $MigrationScript } else { Join-Path $root $MigrationScript }
$output = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $root $OutputPath }
$started = [DateTime]::UtcNow
$dbName = "identity_migration_verify_$([guid]::NewGuid().ToString('N').Substring(0, 12))"
$status = 'fail'
$failure = $null
$historyRows = $null
$brinIndexes = $null
$userListingIndexes = $null
$rerunSucceeded = $false

function Write-Evidence {
    $parent = Split-Path -Parent $output
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    $doc = [ordered]@{
        status = $status
        generatedAtUtc = $started.ToString('o')
        target = 'temporary PostgreSQL database'
        migrationScript = $MigrationScript
        migrationHistoryRows = $historyRows
        brinIndexesVerified = $brinIndexes
        userListingIndexesVerified = $userListingIndexes
        idempotentRerunVerified = $rerunSucceeded
        rtoSeconds = [math]::Round(([DateTime]::UtcNow - $started).TotalSeconds, 3)
        failure = $failure
    }
    [IO.File]::WriteAllText($output, ($doc | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
}

try {
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) { throw "Migration script not found: $scriptPath" }
    if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
        if ($RequireLive) { throw 'psql is required for live migration dry-run.' }
        $status = 'environment-blocked'
        Write-Evidence
        Write-Output 'Identity migration dry-run environment-blocked: psql is not installed.'
        exit 70
    }
    if ([string]::IsNullOrWhiteSpace($Password)) {
        if ($RequireLive) { throw 'Password is required; provide IDENTITY_MIGRATION_TEST_PASSWORD without printing it.' }
        $status = 'environment-blocked'
        Write-Evidence
        Write-Output 'Identity migration dry-run environment-blocked: no test database password supplied.'
        exit 70
    }

    $env:PGPASSWORD = $Password
    & psql --no-psqlrc --set=ON_ERROR_STOP=1 --host=$HostName --port=$Port --username=$Username --dbname=postgres --command="CREATE DATABASE $dbName" *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create temporary migration database.' }
    Get-Content -LiteralPath $scriptPath -Raw | & psql --no-psqlrc --set=ON_ERROR_STOP=1 --host=$HostName --port=$Port --username=$Username --dbname=$dbName *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Identity idempotent migration failed on an empty database.' }
    $verification = & psql --no-psqlrc --no-align --tuples-only --host=$HostName --port=$Port --username=$Username --dbname=$dbName --command='SELECT COUNT(*) FROM "__EFMigrationsHistory"; SELECT COUNT(*) FROM pg_indexes WHERE indexname IN (''ix_security_events_timestamp_brin'',''ix_audit_logs_timestamp_brin''); SELECT COUNT(*) FROM pg_indexes WHERE indexname IN (''ix_asp_net_users_created_at_id'',''ix_asp_net_users_active_created_at_id'');'
    if ($LASTEXITCODE -ne 0) { throw 'Migration verification query failed.' }
    $values = @($verification | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { [int64]$_.Trim() })
    if ($values.Count -lt 3 -or $values[0] -lt 1 -or $values[1] -ne 2 -or $values[2] -ne 2) { throw "Migration verification returned unexpected results." }
    $historyRows = $values[0]
    $brinIndexes = $values[1]
    $userListingIndexes = $values[2]
    # Apply the exact artifact a second time. This catches non-idempotent SQL
    # that an empty-database smoke test cannot detect.
    Get-Content -LiteralPath $scriptPath -Raw | & psql --no-psqlrc --set=ON_ERROR_STOP=1 --host=$HostName --port=$Port --username=$Username --dbname=$dbName *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Identity idempotent migration failed on the already-migrated database.' }
    $rerunVerification = & psql --no-psqlrc --no-align --tuples-only --host=$HostName --port=$Port --username=$Username --dbname=$dbName --command='SELECT COUNT(*) FROM "__EFMigrationsHistory"; SELECT COUNT(*) FROM pg_indexes WHERE indexname IN (''ix_security_events_timestamp_brin'',''ix_audit_logs_timestamp_brin''); SELECT COUNT(*) FROM pg_indexes WHERE indexname IN (''ix_asp_net_users_created_at_id'',''ix_asp_net_users_active_created_at_id'');'
    if ($LASTEXITCODE -ne 0) { throw 'Migration verification query failed after idempotent rerun.' }
    $rerunValues = @($rerunVerification | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { [int64]$_.Trim() })
    if ($rerunValues.Count -lt 3 -or $rerunValues[0] -ne $historyRows -or $rerunValues[1] -ne $brinIndexes -or $rerunValues[2] -ne $userListingIndexes) {
        throw "Idempotent rerun changed migration metadata or required index counts."
    }
    $rerunSucceeded = $true
    $status = 'pass'
    Write-Evidence
    Write-Output "Identity migration dry-run PASS: historyRows=$historyRows brinIndexes=$brinIndexes userListingIndexes=$userListingIndexes rerun=pass"
}
catch {
    $failure = $_.Exception.Message -replace '(?i)(password|token|secret|sig)=[^\s;]*', '$1=[redacted]'
    Write-Evidence
    throw $failure
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($Password)) {
        & psql --no-psqlrc --set=ON_ERROR_STOP=0 --host=$HostName --port=$Port --username=$Username --dbname=postgres --command="DROP DATABASE IF EXISTS $dbName" *> $null
    }
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}
