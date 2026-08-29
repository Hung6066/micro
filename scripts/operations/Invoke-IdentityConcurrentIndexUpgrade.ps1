[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$ConnectionStringFile,
    [int]$StatementTimeoutSeconds = 900,
    [string]$OutputPath = 'artifacts/evidence/identity-concurrent-index-upgrade.json',
    [switch]$RequireLive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($ConnectionString) -and -not [string]::IsNullOrWhiteSpace($ConnectionStringFile)) {
    $filePath = if ([IO.Path]::IsPathRooted($ConnectionStringFile)) { $ConnectionStringFile } else { Join-Path (Get-Location) $ConnectionStringFile }
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { throw "Connection string file not found: $filePath" }
    $document = Get-Content -LiteralPath $filePath -Raw | ConvertFrom-Json
    foreach ($property in @('Identity', 'identity', 'ConnectionString', 'connectionString', 'Default')) {
        if ($null -ne $document.PSObject.Properties[$property] -and -not [string]::IsNullOrWhiteSpace([string]$document.$property)) {
            $ConnectionString = [string]$document.$property
            break
        }
    }
}
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = [Environment]::GetEnvironmentVariable('IDENTITY_DATABASE_CONNECTION_STRING')
}
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    if ($RequireLive) { throw 'ConnectionString, ConnectionStringFile or IDENTITY_DATABASE_CONNECTION_STRING is required.' }
    Write-Output 'Identity concurrent index upgrade environment-blocked: no connection supplied.'
    exit 70
}

if ($StatementTimeoutSeconds -lt 30 -or $StatementTimeoutSeconds -gt 3600) {
    throw 'StatementTimeoutSeconds must be between 30 and 3600.'
}
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
    if ($RequireLive) { throw 'psql is required for the concurrent index upgrade.' }
    Write-Output 'Identity concurrent index upgrade environment-blocked: psql is not installed.'
    exit 70
}

$sqlStatements = @(
    "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_security_events_timestamp_brin ON security_events USING BRIN (timestamp);",
    "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_audit_logs_timestamp_brin ON audit_logs USING BRIN (timestamp);",
    "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_asp_net_users_created_at_id ON asp_net_users (created_at, id);",
    "CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_asp_net_users_active_created_at_id ON asp_net_users (is_active, created_at, id);"
)

$previousPgOptions = $env:PGOPTIONS
try {
    $env:PGOPTIONS = "-c statement_timeout=$($StatementTimeoutSeconds)s"
    foreach ($sql in $sqlStatements) {
        & psql --no-psqlrc --set=ON_ERROR_STOP=1 --dbname=$ConnectionString --command=$sql *> $null
        if ($LASTEXITCODE -ne 0) { throw 'Concurrent Identity index creation failed.' }
    }
    $count = (& psql --no-psqlrc --no-align --tuples-only --set=ON_ERROR_STOP=1 --dbname=$ConnectionString --command="SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'public' AND indexname IN ('ix_security_events_timestamp_brin','ix_audit_logs_timestamp_brin','ix_asp_net_users_created_at_id','ix_asp_net_users_active_created_at_id');").Trim()
    if ($LASTEXITCODE -ne 0 -or [int]$count -ne 4) { throw "Expected 4 Identity scale indexes, found $count." }
    $result = [pscustomobject]@{ status = 'pass'; verifiedIndexes = [int]$count; statementTimeoutSeconds = $StatementTimeoutSeconds; generatedAtUtc = [DateTime]::UtcNow.ToString('o') }
    $fullOutput = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path (Get-Location) $OutputPath }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullOutput) | Out-Null
    [IO.File]::WriteAllText($fullOutput, ($result | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
    Write-Output "Identity concurrent index upgrade PASS: verified $count scale indexes."
} catch {
    throw $_
} finally {
    if ($null -eq $previousPgOptions) { Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
    else { $env:PGOPTIONS = $previousPgOptions }
}
