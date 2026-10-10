[CmdletBinding()]
param(
    [string]$IamScopesFile = 'config/conglomerate/iam-scopes.v1.json',
    [string]$OidcClientsFile = 'config/conglomerate/oidc-clients.azure-staging.json',
    [string]$CustomerTenantsFile = 'config/conglomerate/customer-tenants.v1.json',
    [string]$TenantPlacementFile = 'config/conglomerate/tenant-placement.v1.json',
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$iamPath = Join-Path $repoRoot $IamScopesFile
$oidcPath = Join-Path $repoRoot $OidcClientsFile
$customerPath = Join-Path $repoRoot $CustomerTenantsFile

foreach ($file in @($iamPath, $oidcPath, (Join-Path $repoRoot 'config/conglomerate/seed-data.v1.json'))) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Config not found: $file" }
}

$iam = Get-Content -LiteralPath $iamPath -Raw | ConvertFrom-Json
$oidc = Get-Content -LiteralPath $oidcPath -Raw | ConvertFrom-Json
$tenantKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($tenant in $iam.tenants) { [void]$tenantKeys.Add($tenant.key) }

if (Test-Path -LiteralPath $customerPath) {
    $customers = Get-Content -LiteralPath $customerPath -Raw | ConvertFrom-Json
    foreach ($customer in $customers.customers) {
        if ($customer.key -in $tenantKeys) {
            throw "Customer tenant key '$($customer.key)' conflicts with internal tenant key."
        }
        [void]$tenantKeys.Add($customer.key)
        if ([string]::IsNullOrWhiteSpace($customer.operatorHome)) {
            throw "Customer tenant '$($customer.key)' requires operatorHome."
        }
        if ($customer.operatorHome -notin @($iam.tenants.key)) {
            throw "Customer tenant '$($customer.key)' references unknown operatorHome '$($customer.operatorHome)'."
        }
        foreach ($portalClient in $customer.portalClients) {
            if ([string]::IsNullOrWhiteSpace($portalClient.clientId)) {
                throw "Customer tenant '$($customer.key)' has a portal client without clientId."
            }
            if ($portalClient.portalClass -notin @('customer_operator', 'end_user')) {
                throw "Portal client '$($portalClient.clientId)' must use customer_operator or end_user."
            }
        }
    }
}

$placementPath = Join-Path $repoRoot $TenantPlacementFile
if (Test-Path -LiteralPath $placementPath) {
    $placement = Get-Content -LiteralPath $placementPath -Raw | ConvertFrom-Json
    if ($placement.defaultTier -notin @('shared', 'dedicated')) {
        throw "Tenant placement defaultTier must be 'shared' or 'dedicated'."
    }
    foreach ($entry in $placement.placements) {
        if ([string]::IsNullOrWhiteSpace($entry.tenantKey)) {
            throw 'Tenant placement entry requires tenantKey.'
        }
        if ($entry.tier -notin @('shared', 'dedicated')) {
            throw "Tenant placement '$($entry.tenantKey)' has invalid tier '$($entry.tier)'."
        }
        if ($entry.tier -eq 'dedicated') {
            if (-not $entry.services -or @($entry.services.PSObject.Properties).Count -eq 0) {
                throw "Dedicated placement '$($entry.tenantKey)' requires at least one services binding."
            }
            foreach ($serviceBinding in $entry.services.PSObject.Properties) {
                if ([string]::IsNullOrWhiteSpace($serviceBinding.Value.connectionName)) {
                    throw "Dedicated placement '$($entry.tenantKey)' service '$($serviceBinding.Name)' requires connectionName."
                }
            }
        }
        if ($entry.tier -eq 'dedicated' -and $entry.active -and -not $placement.enabled) {
            Write-Warning "Dedicated placement '$($entry.tenantKey)' is active while enabled=false (expected until contract/compliance approval)."
        }
        if ($entry.tenantKey -notin $tenantKeys) {
            throw "Tenant placement '$($entry.tenantKey)' references unknown tenant key."
        }
    }
}

foreach ($client in $oidc.clients) {
    if ($client.tenantKey -notin $tenantKeys) {
        throw "OIDC client '$($client.clientId)' references unknown tenant '$($client.tenantKey)'."
    }
    if ($client.redirectUris.Count -lt 1) {
        throw "OIDC client '$($client.clientId)' has no redirect URIs."
    }
    if ($client.PSObject.Properties.Name -contains 'portalClass') {
        if ($client.portalClass -notin @('operator', 'customer_operator', 'end_user')) {
            throw "OIDC client '$($client.clientId)' has invalid portalClass '$($client.portalClass)'."
        }
    }
}

Write-Output @{
    validatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    organization = $iam.organization.key
    tenants = @($tenantKeys)
    oidcClients = @($oidc.clients | ForEach-Object { $_.clientId })
    customerTenantsFile = $(if (Test-Path -LiteralPath $customerPath) { $CustomerTenantsFile } else { $null })
    tenantPlacementFile = $(if (Test-Path -LiteralPath $placementPath) { $TenantPlacementFile } else { $null })
    validateOnly = [bool]$ValidateOnly
    message = 'Conglomerate IAM/OIDC/customer-tenant config is consistent. Enable Conglomerate:Enabled and CustomerTenantsPath in appsettings.Azure.Staging.json, then restart Identity to seed scopes.'
} | ConvertTo-Json -Depth 5
