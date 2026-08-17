[CmdletBinding()]
param(
    [string]$Namespace = 'his-hope',
    [switch]$RequireVaultReachable
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function KText {
    param([Parameter(Mandatory)][string[]]$Args)
    $value = & kubectl @Args
    if ($LASTEXITCODE -ne 0) { throw "kubectl failed: kubectl $($Args -join ' ')" }
    $value -join "`n"
}

$driver = KText @('get', 'daemonset', '-n', 'kube-system', 'csi-secrets-store-secrets-store-csi-driver', '-o', 'jsonpath={.status.numberReady}/{.status.desiredNumberScheduled}')
if ($driver -notmatch '^\d+/\d+$' -or ($driver -split '/')[0] -ne ($driver -split '/')[1]) {
    throw "Secrets Store CSI driver is not ready: $driver"
}

$provider = KText @('get', 'daemonset', '-n', $Namespace, 'vault-csi-csi-provider', '-o', 'jsonpath={.status.numberReady}/{.status.desiredNumberScheduled}')
if ($provider -notmatch '^\d+/\d+$' -or ($provider -split '/')[0] -ne ($provider -split '/')[1]) {
    throw "Vault CSI provider is not ready: $provider"
}

$classes = @(($(KText @('get', 'secretproviderclass', '-n', $Namespace, '-o', 'name')) -split "`n") | Where-Object { $_ })
if ($classes.Count -lt 4) { throw "Expected production SecretProviderClass objects; found $($classes.Count)." }

$endpointSlices = & kubectl get endpointslice -n $Namespace -l kubernetes.io/service-name=vault-active -o json 2>$null | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { $endpointSlices = $null }
$vaultEndpoints = @($endpointSlices.items | ForEach-Object { $_.endpoints } | Where-Object { $_.conditions.ready -eq $true } | ForEach-Object { $_.addresses })
if ([string]::IsNullOrWhiteSpace($vaultEndpoints)) {
    if ($RequireVaultReachable) { throw 'Production Vault service has no ready endpoints.' }
    Write-Output "Vault secret platform PARTIAL: CSI driver=$driver, provider=$provider, SecretProviderClass=$($classes.Count), but vault-active has no ready endpoint."
    exit 0
}

Write-Output "Vault secret platform PASS: CSI driver=$driver, provider=$provider, SecretProviderClass=$($classes.Count), Vault endpoints=$vaultEndpoints."
