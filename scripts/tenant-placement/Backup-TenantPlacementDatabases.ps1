[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$TenantKey,
    [Parameter(Mandatory)]
    [string]$ConnectionStringsFile,
    [string]$PlacementFile = 'config/conglomerate/tenant-placement.v1.json',
    [string]$OutputDirectory = (Join-Path (Get-TenantPlacementRepoRoot) 'artifacts/tenant-placement-backups'),
    [string]$PgDumpPath = 'pg_dump',
    [switch]$IncludeInactive
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TenantPlacementOps.Common.ps1')

$placement = Read-TenantPlacementConfig -PlacementFile $PlacementFile
$bindings = Get-TenantPlacementDedicatedBindings -PlacementConfig $placement.Config -TenantKey $TenantKey -IncludeInactive:$IncludeInactive
$connectionMap = Read-TenantPlacementConnectionStrings -ConnectionStringsFile $ConnectionStringsFile

$backups = @()
foreach ($binding in $bindings) {
    if (-not $connectionMap.ContainsKey($binding.ConnectionName)) {
        throw "Missing connection string for '$($binding.ConnectionName)' in $ConnectionStringsFile"
    }

    $backups += Invoke-TenantPlacementDatabaseBackup `
        -ConnectionName $binding.ConnectionName `
        -ConnectionString $connectionMap[$binding.ConnectionName] `
        -OutputDirectory $OutputDirectory `
        -PgDumpPath $PgDumpPath
}

[pscustomobject]@{
    tenantKey = $TenantKey
    backedUpAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    outputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    backups = @($backups)
} | ConvertTo-Json -Depth 5
