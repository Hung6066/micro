[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$ConnectionString,
    [string]$ConnectionStringFile,
    [string]$OutputPath = 'artifacts/evidence/identity-scale-readiness.json',
    [int64]$RowWarningThreshold = 100000000,
    [int64]$TableSizeWarningBytes = 107374182400,
    [int]$ServiceReplicaCount = 3,
    [int]$PoolMaxPerReplica = 20,
    [int]$ReservedConnections = 20,
    [switch]$RequireLive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Add-Check([System.Collections.Generic.List[object]]$Checks, [string]$Name, [string]$Status, [string]$Message, [object]$Value = $null) {
    $Checks.Add([pscustomobject]@{ name = $Name; status = $Status; message = $Message; value = $Value })
}

function Resolve-ConnectionString {
    if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) { return $ConnectionString }
    $environmentValue = [Environment]::GetEnvironmentVariable('IDENTITY_DATABASE_CONNECTION_STRING')
    if (-not [string]::IsNullOrWhiteSpace($environmentValue)) { return $environmentValue }
    if (-not [string]::IsNullOrWhiteSpace($ConnectionStringFile)) {
        $path = if ([IO.Path]::IsPathRooted($ConnectionStringFile)) { $ConnectionStringFile } else { Join-Path $RepositoryRoot $ConnectionStringFile }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Connection string file not found: $path" }
        $raw = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        foreach ($property in @('Identity', 'identity', 'ConnectionString', 'connectionString', 'Default')) {
            if ($null -ne $raw.PSObject.Properties[$property] -and -not [string]::IsNullOrWhiteSpace([string]$raw.$property)) { return [string]$raw.$property }
        }
        throw 'Connection string file has no Identity/ConnectionString entry.'
    }
    return $null
}

function ConvertTo-PsqlConnectionString([string]$Value) {
    # Npgsql-style connection strings are common in this repository, while
    # libpq expects lowercase keyword names (notably dbname/user).
    if ($Value -notmatch '=') { return $Value }
    $mapping = @{ host = 'host'; server = 'host'; port = 'port'; database = 'dbname'; dbname = 'dbname'; username = 'user'; user = 'user'; password = 'password'; sslmode = 'sslmode' }
    $parts = [System.Collections.Generic.List[string]]::new()
    foreach ($segment in ($Value -split ';')) {
        if ([string]::IsNullOrWhiteSpace($segment)) { continue }
        $pair = $segment -split '=', 2
        if ($pair.Count -ne 2) { continue }
        $key = $pair[0].Trim().ToLowerInvariant()
        if ($mapping.ContainsKey($key)) { $parts.Add("$($mapping[$key])=$($pair[1].Trim())") }
    }
    if ($parts.Count -eq 0) { return $Value }
    return ($parts -join ' ')
}

Push-Location $RepositoryRoot
try {
    $checks = [System.Collections.Generic.List[object]]::new()
    if ($ServiceReplicaCount -lt 1 -or $PoolMaxPerReplica -lt 1 -or $ReservedConnections -lt 0) {
        throw 'ServiceReplicaCount and PoolMaxPerReplica must be positive; ReservedConnections must be non-negative.'
    }
    $applicationConnectionBudget = [int64]$ServiceReplicaCount * $PoolMaxPerReplica
    $totalConnectionBudget = $applicationConnectionBudget + $ReservedConnections
    Add-Check $checks 'connection-budget-model' 'pass' "application=$applicationConnectionBudget; reserved=$ReservedConnections; total=$totalConnectionBudget" ([pscustomobject]@{ replicas = $ServiceReplicaCount; poolMaxPerReplica = $PoolMaxPerReplica; reserved = $ReservedConnections; total = $totalConnectionBudget })
    $connection = Resolve-ConnectionString
    $live = -not [string]::IsNullOrWhiteSpace($connection)

    if (-not $live) {
        Add-Check $checks 'live-database' ($(if ($RequireLive) { 'fail' } else { 'environment-blocked' })) 'No connection supplied; static configuration checks can still run.'
    } elseif (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
        Add-Check $checks 'live-database' 'environment-blocked' 'psql is not installed on this host; install PostgreSQL client for live capacity evidence.'
        $live = $false
    } else {
        $sql = @'
SELECT json_build_object(
  'server_version', current_setting('server_version'),
  'max_connections', current_setting('max_connections')::int,
  'tables', COALESCE((SELECT json_agg(row_to_json(t)) FROM (
    SELECT schemaname, relname, n_live_tup::bigint AS estimated_rows,
           pg_total_relation_size(relid)::bigint AS total_bytes,
           n_dead_tup::bigint AS dead_rows,
           CASE WHEN n_live_tup > 0 THEN round((n_dead_tup::numeric / n_live_tup) * 100, 2) ELSE 0 END AS dead_ratio_pct
    FROM pg_stat_user_tables
    WHERE schemaname = 'public'
      AND relname IN ('asp_net_users','audit_logs','security_events','openiddict_tokens','security_signal_outbox','directory_provisioning_outbox')
    ORDER BY pg_total_relation_size(relid) DESC
  ) t), '[]'::json)
)::text;
'@
        $psqlConnection = ConvertTo-PsqlConnectionString $connection
        $jsonText = (& psql --no-psqlrc --no-align --tuples-only --quiet --dbname=$psqlConnection --command=$sql 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0) { throw "Live identity capacity query failed: $jsonText" }
        $snapshot = $jsonText | ConvertFrom-Json
        Add-Check $checks 'live-database' 'pass' 'Live PostgreSQL capacity snapshot collected.'
        Add-Check $checks 'server-version' 'pass' ([string]$snapshot.server_version)
        Add-Check $checks 'max-connections' 'pass' "max_connections=$($snapshot.max_connections)" ([int]$snapshot.max_connections)
        if ($totalConnectionBudget -ge [int]$snapshot.max_connections) {
            Add-Check $checks 'connection-budget-headroom' 'fail' "Configured connection budget $totalConnectionBudget must remain below max_connections=$($snapshot.max_connections)."
        } else {
            Add-Check $checks 'connection-budget-headroom' 'pass' "Connection budget $totalConnectionBudget leaves $([int]$snapshot.max_connections - $totalConnectionBudget) connections of headroom."
        }

        $indexSql = "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND indexname IN ('ix_security_events_timestamp_brin','ix_audit_logs_timestamp_brin','ix_asp_net_users_created_at_id','ix_asp_net_users_active_created_at_id');"
        $indexText = (& psql --no-psqlrc --no-align --tuples-only --quiet --dbname=$psqlConnection --command=$indexSql 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0) { throw "Live identity index verification failed: $indexText" }
        $indexCount = [int]$indexText
        if ($indexCount -ne 4) {
            Add-Check $checks 'required-scale-indexes' 'fail' "Expected 4 scale indexes, found $indexCount." $indexCount
        } else {
            Add-Check $checks 'required-scale-indexes' 'pass' 'BRIN audit/security and user listing indexes are present.' $indexCount
        }

        foreach ($table in @($snapshot.tables)) {
            $rows = [int64]$table.estimated_rows
            $bytes = [int64]$table.total_bytes
            $status = if ($rows -ge $RowWarningThreshold -or $bytes -ge $TableSizeWarningBytes) { 'warning' } else { 'pass' }
            $message = "rows=$rows; sizeBytes=$bytes; deadRatioPct=$($table.dead_ratio_pct)"
            Add-Check $checks "table:$($table.relname)" $status $message $table
        }
    }

    $requiredFiles = @(
        'src/Shared/Persistence/His.Hope.Persistence/HisHopeDatabaseOptions.cs',
        'src/Shared/Persistence/His.Hope.Persistence/MigrationRunner.cs',
        'scripts/validate-identity-migration-safety.ps1',
        'docs/operations/identity-scale-migration-runbook.vi.md'
    )
    foreach ($relativePath in $requiredFiles) {
        if (Test-Path -LiteralPath (Join-Path $RepositoryRoot $relativePath) -PathType Leaf) {
            Add-Check $checks "artifact:$relativePath" 'pass' 'Required scale-safety artifact exists.'
        } else {
            Add-Check $checks "artifact:$relativePath" 'fail' 'Required scale-safety artifact is missing.'
        }
    }

    $failed = @($checks | Where-Object status -eq 'fail')
    $blocked = @($checks | Where-Object status -eq 'environment-blocked')
    $result = [pscustomobject]@{
        status = if ($failed.Count -gt 0) { 'fail' } elseif ($blocked.Count -gt 0) { 'environment-blocked' } else { 'pass' }
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        thresholds = [pscustomobject]@{ rowWarning = $RowWarningThreshold; tableSizeWarningBytes = $TableSizeWarningBytes; replicas = $ServiceReplicaCount; poolMaxPerReplica = $PoolMaxPerReplica; reservedConnections = $ReservedConnections }
        checks = @($checks)
    }
    $fullOutputPath = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $RepositoryRoot $OutputPath }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullOutputPath) | Out-Null
    [IO.File]::WriteAllText($fullOutputPath, ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    $result | ConvertTo-Json -Depth 8
    if ($failed.Count -gt 0) { exit 80 }
    if ($blocked.Count -gt 0) { exit 70 }
} finally {
    Pop-Location
}
