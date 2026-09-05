[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$BackupFile,
    [Parameter(Mandatory)] [string]$ChecksumFile,
    [string]$PgRestorePath = 'pg_restore'
)

$ErrorActionPreference = 'Stop'
$backup = [IO.Path]::GetFullPath($BackupFile)
$checksum = [IO.Path]::GetFullPath($ChecksumFile)
if (-not (Test-Path -LiteralPath $backup -PathType Leaf)) { throw "Backup file not found: $backup" }
if (-not (Test-Path -LiteralPath $checksum -PathType Leaf)) { throw "Checksum file not found: $checksum" }

$metadata = Get-Content -LiteralPath $checksum -Raw | ConvertFrom-Json
$actual = (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
if ($actual -ne $metadata.Sha256) { throw "SHA256 mismatch for '$backup'." }

# Listing the archive validates that pg_restore can parse the custom-format
# backup without mutating a database. A full restore must run in an isolated
# recovery database using the approved DR runbook and change ticket.
& $PgRestorePath --list --file=$backup | Out-Null
if ($LASTEXITCODE -ne 0) { throw "pg_restore archive validation failed for '$backup'." }

[pscustomobject]@{
    BackupFile = $backup
    Sha256Verified = $true
    ArchiveReadable = $true
    ValidatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
} | ConvertTo-Json
