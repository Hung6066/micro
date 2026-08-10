[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [string]$Database,
    [Parameter(Mandatory)] [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })] [string]$BackupFile,
    [switch]$ConfirmRestore,
    [string]$PgRestorePath = 'pg_restore'
)

$ErrorActionPreference = 'Stop'
if (-not $ConfirmRestore) {
    throw 'Restore is destructive. Re-run with -ConfirmRestore after validating the target database and backup checksum.'
}

$resolvedBackup = [IO.Path]::GetFullPath($BackupFile)
if ($PSCmdlet.ShouldProcess($Database, "Restore $resolvedBackup")) {
    & $PgRestorePath --clean --if-exists --no-owner --no-acl --dbname=$Database $resolvedBackup
    if ($LASTEXITCODE -ne 0) { throw "pg_restore failed for database '$Database'." }
    Write-Output "Restore completed: $Database"
}
