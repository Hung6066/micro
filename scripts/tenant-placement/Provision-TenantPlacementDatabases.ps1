[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$TenantKey,
    [Parameter(Mandatory)]
    [string]$ConnectionStringsFile,
    [string]$PlacementFile = 'config/conglomerate/tenant-placement.v1.json',
    [switch]$SkipMigrate,
    [switch]$IncludeInactive
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TenantPlacementOps.Common.ps1')

$placement = Read-TenantPlacementConfig -PlacementFile $PlacementFile
$bindings = Get-TenantPlacementDedicatedBindings -PlacementConfig $placement.Config -TenantKey $TenantKey -IncludeInactive:$IncludeInactive
$connectionMap = Read-TenantPlacementConnectionStrings -ConnectionStringsFile $ConnectionStringsFile

$results = @()
foreach ($binding in $bindings) {
    if (-not $connectionMap.ContainsKey($binding.ConnectionName)) {
        throw "Missing connection string for '$($binding.ConnectionName)' in $ConnectionStringsFile"
    }

    $connectionString = $connectionMap[$binding.ConnectionName]
    if ($PSCmdlet.ShouldProcess($binding.ConnectionName, 'Ensure PostgreSQL database')) {
        $results += Ensure-PostgresDatabase -ConnectionString $connectionString
    } else {
        $results += Ensure-PostgresDatabase -ConnectionString $connectionString -WhatIf
    }

    if (-not $SkipMigrate -and $PSCmdlet.ShouldProcess($binding.ServiceName, 'Apply EF migrations')) {
        $results += Invoke-TenantPlacementEfMigrate -ServiceName $binding.ServiceName -ConnectionString $connectionString
    } elseif (-not $SkipMigrate) {
        $results += Invoke-TenantPlacementEfMigrate -ServiceName $binding.ServiceName -ConnectionString $connectionString -WhatIf
    }
}

[pscustomobject]@{
    tenantKey = $TenantKey
    provisionedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    skipMigrate = [bool]$SkipMigrate
    results = @($results)
    nextSteps = @(
        'Register ConnectionStrings in target service configuration (Key Vault / appsettings overlay).',
        'Add or activate placement entry in tenant-placement.v1.json.',
        'Set TenantPlacement:Enabled=true only after contract/compliance approval ticket is linked in reason.',
        'Restart services and verify /health/ready on each affected API.'
    )
} | ConvertTo-Json -Depth 5
