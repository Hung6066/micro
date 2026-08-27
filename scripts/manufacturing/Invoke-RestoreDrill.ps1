param(
    [Parameter(Mandatory)] [string]$BackupFile,
    [string]$PostgresContainer = 'his-hope-postgres-restore-drill',
    [string]$DatabasePrefix = 'manufacturing_restore_drill',
    [int]$MaxRtoSeconds = 300,
    [int]$MaxRpoMinutes = 15,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
if ($MaxRtoSeconds -lt 1) { throw 'MaxRtoSeconds must be positive.' }
if ($MaxRpoMinutes -lt 0) { throw 'MaxRpoMinutes cannot be negative.' }
if ($DatabasePrefix -notmatch '^[A-Za-z_][A-Za-z0-9_]{0,40}$') {
    throw 'DatabasePrefix must be a PostgreSQL identifier (letters, digits, underscore; max 41 characters).'
}
$backup = [IO.Path]::GetFullPath($BackupFile)
if (-not (Test-Path -LiteralPath $backup -PathType Leaf)) { throw "Backup file not found: $backup" }
$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$backupTimestamp = (Get-Item -LiteralPath $backup).LastWriteTimeUtc
$name = "${DatabasePrefix}_$((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss'))"
& docker cp $backup "${PostgresContainer}:/tmp/$name.dump"
if ($LASTEXITCODE -ne 0) { throw 'docker cp failed.' }
& docker exec $PostgresContainer psql -U postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $name"
if ($LASTEXITCODE -ne 0) { throw "Could not create isolated database '$name'." }
& docker exec $PostgresContainer pg_restore -U postgres --no-owner --no-acl --dbname=$name "/tmp/$name.dump"
if ($LASTEXITCODE -ne 0) { throw "pg_restore failed for '$name'." }
$lots = (& docker exec $PostgresContainer psql -U postgres -d $name -Atc 'select count(*) from manufacturing_lots;').Trim()
$outbox = (& docker exec $PostgresContainer psql -U postgres -d $name -Atc 'select count(*) from manufacturing_outbox_messages;').Trim()
if ([int]$lots -lt 1 -or [int]$outbox -lt 1) { throw "Restore verification failed: lots=$lots outbox=$outbox" }
$stopwatch.Stop()
$verifiedAt = (Get-Date).ToUniversalTime()
$rtoSeconds = [math]::Round($stopwatch.Elapsed.TotalSeconds, 2)
$rpoMinutes = [math]::Round(($verifiedAt - $backupTimestamp).TotalMinutes, 2)
$rtoPass = $rtoSeconds -le $MaxRtoSeconds
$rpoPass = $rpoMinutes -le $MaxRpoMinutes
$result = [pscustomobject]@{
    Database = $name
    Lots = [int]$lots
    OutboxMessages = [int]$outbox
    BackupTimestampUtc = $backupTimestamp.ToString('o')
    VerifiedAtUtc = $verifiedAt.ToString('o')
    RtoSeconds = $rtoSeconds
    RpoMinutesMeasured = $rpoMinutes
    MaxRtoSeconds = $MaxRtoSeconds
    MaxRpoMinutes = $MaxRpoMinutes
    RtoPass = $rtoPass
    RpoPass = $rpoPass
    # Stable evidence contract consumed by validate-dr-evidence.ps1.
    status = if ($rtoPass -and $rpoPass) { 'pass' } else { 'fail' }
    executedAtUtc = $verifiedAt.ToString('o')
    rtoMinutes = [math]::Round($rtoSeconds / 60, 4)
    rpoMinutes = $rpoMinutes
    restoreVerified = $true
    target = $PostgresContainer
}
$json = $result | ConvertTo-Json
if ($OutputPath) {
    $outputDirectory = Split-Path -Parent $OutputPath
    if ($outputDirectory) { New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if (-not $rtoPass -or -not $rpoPass) { exit 2 }
