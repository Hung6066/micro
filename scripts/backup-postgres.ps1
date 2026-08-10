[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Database,
    [string]$OutputDirectory = (Join-Path (Get-Location) 'artifacts/database-backups'),
    [string]$PgDumpPath = 'pg_dump'
)

$ErrorActionPreference = 'Stop'
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
$stamp = Get-Date -AsUTC -Format 'yyyyMMddTHHmmssZ'
$file = Join-Path $resolvedOutput "$Database-$stamp.dump"

# Authentication must come from libpq environment/.pgpass/Vault. Never put a
# password in this command line or commit it to a script.
& $PgDumpPath --format=custom --no-owner --no-acl --file=$file --dbname=$Database
if ($LASTEXITCODE -ne 0) { throw "pg_dump failed for database '$Database'." }

$hash = Get-FileHash -LiteralPath $file -Algorithm SHA256
[pscustomobject]@{ Database = $Database; File = $file; Sha256 = $hash.Hash; CreatedAtUtc = $stamp } |
    ConvertTo-Json | Set-Content -LiteralPath "$file.sha256.json" -Encoding utf8

Write-Output "Backup created: $file"
