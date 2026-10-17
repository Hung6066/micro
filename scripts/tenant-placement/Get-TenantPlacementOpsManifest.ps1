[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$TenantKey,
    [string]$PlacementFile = 'config/conglomerate/tenant-placement.v1.json',
    [string]$ConnectionStringsFile,
    [switch]$IncludeInactive
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TenantPlacementOps.Common.ps1')

$placement = Read-TenantPlacementConfig -PlacementFile $PlacementFile
$bindings = Get-TenantPlacementDedicatedBindings -PlacementConfig $placement.Config -TenantKey $TenantKey -IncludeInactive:$IncludeInactive
$connectionMap = @{}
if ($ConnectionStringsFile) {
    $connectionMap = Read-TenantPlacementConnectionStrings -ConnectionStringsFile $ConnectionStringsFile
}

$services = foreach ($binding in $bindings) {
    $connectionStringConfigured = $connectionMap.ContainsKey($binding.ConnectionName)
    [pscustomobject]@{
        serviceName = $binding.ServiceName
        connectionName = $binding.ConnectionName
        connectionStringConfigured = $connectionStringConfigured
        database = if ($connectionStringConfigured) {
            Get-PostgresDatabaseNameFromConnectionString -ConnectionString $connectionMap[$binding.ConnectionName]
        } else { $null }
    }
}

[pscustomobject]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    placementFile = $PlacementFile
    routingEnabled = [bool]$placement.Config.enabled
    tenantKey = $TenantKey
    tier = $bindings[0].Tier
    active = [bool]$bindings[0].Active
    dataRegion = $bindings[0].DataRegion
    reason = $bindings[0].Reason
    services = @($services)
} | ConvertTo-Json -Depth 5
