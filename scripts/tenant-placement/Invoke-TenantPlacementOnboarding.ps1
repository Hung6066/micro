[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$TenantKey,
    [string]$PlacementFile = 'config/conglomerate/tenant-placement.v1.json',
    [string]$ConnectionStringsFile,
    [ValidateSet('Validate', 'Provision', 'Backup')]
    [string]$Phase = 'Validate',
    [string]$BackupOutputDirectory,
    [switch]$IncludeInactive
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TenantPlacementOps.Common.ps1')

if ($Phase -ne 'Validate' -and [string]::IsNullOrWhiteSpace($ConnectionStringsFile)) {
    throw 'ConnectionStringsFile is required for Provision and Backup phases.'
}

$manifest = & (Join-Path $PSScriptRoot 'Get-TenantPlacementOpsManifest.ps1') `
    -TenantKey $TenantKey `
    -PlacementFile $PlacementFile `
    -ConnectionStringsFile $ConnectionStringsFile `
    -IncludeInactive:$IncludeInactive | ConvertFrom-Json

if ($Phase -eq 'Validate') {
    if (-not $ConnectionStringsFile) {
        $manifest | Add-Member -NotePropertyName message -NotePropertyValue 'Placement manifest generated. Provide ConnectionStringsFile to verify connection string coverage.' -Force
        $manifest | ConvertTo-Json -Depth 5
        exit 0
    }

    $missing = @($manifest.services | Where-Object { -not $_.connectionStringConfigured } | ForEach-Object { $_.connectionName })
    if ($missing.Count -gt 0) {
        throw "Missing connection strings for: $($missing -join ', ')"
    }

    [pscustomobject]@{
        phase = 'Validate'
        status = 'pass'
        tenantKey = $TenantKey
        routingEnabled = $manifest.routingEnabled
        active = $manifest.active
        validatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        services = $manifest.services
        message = 'Dedicated placement manifest and connection strings are consistent.'
    } | ConvertTo-Json -Depth 5
    exit 0
}

if ($Phase -eq 'Provision') {
    if ($PSCmdlet.ShouldProcess($TenantKey, 'Provision dedicated placement databases')) {
        & (Join-Path $PSScriptRoot 'Provision-TenantPlacementDatabases.ps1') `
            -TenantKey $TenantKey `
            -PlacementFile $PlacementFile `
            -ConnectionStringsFile $ConnectionStringsFile `
            -IncludeInactive:$IncludeInactive
    }
    exit 0
}

$outputDirectory = if ($BackupOutputDirectory) {
    $BackupOutputDirectory
} else {
    Join-Path (Get-TenantPlacementRepoRoot) "artifacts/tenant-placement-backups/$TenantKey"
}

& (Join-Path $PSScriptRoot 'Backup-TenantPlacementDatabases.ps1') `
    -TenantKey $TenantKey `
    -PlacementFile $PlacementFile `
    -ConnectionStringsFile $ConnectionStringsFile `
    -OutputDirectory $outputDirectory `
    -IncludeInactive:$IncludeInactive
