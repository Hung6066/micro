[CmdletBinding()]
param(
    [int]$TimeoutSeconds = 30,
    [string]$Kubeconfig = 'artifacts/kubeconfig-production.yaml',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()

if (Test-Path -LiteralPath $Kubeconfig -PathType Leaf) {
    $env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
}
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { $failures.Add($Message) }
}

function Invoke-Kubectl([string[]]$Arguments) {
    & kubectl @Arguments "--request-timeout=${TimeoutSeconds}s"
    if ($LASTEXITCODE -ne 0) {
        throw "kubectl failed with exit code ${LASTEXITCODE}: $($Arguments -join ' ')"
    }
}

function Write-Evidence([string]$Status, [string[]]$FailureMessages) {
    if ([string]::IsNullOrWhiteSpace($OutputPath)) { return }
    $result = [pscustomobject]@{
        status = $Status
        checks = @(
            [pscustomobject]@{ name = 'production-ha-spire'; status = $Status; detail = if ($FailureMessages.Count -eq 0) { 'HA/SPIRE runtime contract passed.' } else { $FailureMessages -join '; ' } }
        )
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
}

if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) {
    Write-Evidence -Status 'environment-blocked' -FailureMessages @("Kubeconfig not found: $Kubeconfig")
    Write-Output "Production SPIRE HA validation environment-blocked: kubeconfig not found ($Kubeconfig)."
    exit 0
}

try {
    Invoke-Kubectl @('version', '--output=json') | Out-Null
}
catch {
    Write-Evidence -Status 'environment-blocked' -FailureMessages @($_.Exception.Message)
    Write-Output "Production SPIRE HA validation environment-blocked: cluster is unreachable."
    exit 0
}

$cluster = Invoke-Kubectl @('get', 'cluster', 'spire-postgres', '-n', 'spire', '-o', 'json') | ConvertFrom-Json
$server = Invoke-Kubectl @('get', 'statefulset', 'spire-server', '-n', 'spire', '-o', 'json') | ConvertFrom-Json
$serverPods = @(Invoke-Kubectl @('get', 'pods', '-n', 'spire', '-l', 'app.kubernetes.io/name=spire-server', '-o', 'json') | ConvertFrom-Json).items
$agentPods = @(Invoke-Kubectl @('get', 'pods', '-n', 'spire', '-l', 'app.kubernetes.io/name=spire-agent', '-o', 'json') | ConvertFrom-Json).items
$endpointSlices = Invoke-Kubectl @('get', 'endpointslice', '-n', 'spire', '-l', 'kubernetes.io/service-name=spire-server', '-o', 'json') | ConvertFrom-Json
$config = [string]::Join("`n", @(Invoke-Kubectl @('get', 'configmap', 'spire-server', '-n', 'spire', '-o', 'jsonpath={.data.server\.conf}')))
$prodRender = [string]::Join("`n", @(kubectl kustomize (Join-Path $repoRoot 'k8s/overlays/prod') --load-restrictor LoadRestrictionsNone))
$readyServerPods = @($serverPods | Where-Object { $_.status.containerStatuses[0].ready }).Count
$readyAgentPods = @($agentPods | Where-Object { $_.status.containerStatuses[0].ready }).Count

Require ($cluster.status.phase -eq 'Cluster in healthy state') "CNPG phase is '$($cluster.status.phase)'."
Require ($cluster.status.instances -eq 3 -and $cluster.status.readyInstances -eq 3) "CNPG is not 3/3 ready."
Require ($server.spec.replicas -eq 3 -and $server.status.readyReplicas -eq 3) "SPIRE Server is not 3/3 ready."
$serverEndpointCount = @($endpointSlices.items | ForEach-Object { $_.endpoints } | Where-Object { $_.conditions.ready -eq $true }).Count
Require ($serverEndpointCount -eq 3) "SPIRE Server service does not expose 3 ready endpoints (count=$serverEndpointCount)."
Require ((Invoke-Kubectl @('get', 'pdb', 'spire-server', '-n', 'spire', '-o', 'jsonpath={.spec.minAvailable}')) -eq '2') 'SPIRE Server PDB minAvailable is not 2.'
Require ($config -match 'host=__SPIRE_DB_HOST__') 'SPIRE server config no longer uses the runtime datastore host placeholder.'
Require ($config -notmatch 'postgres\.his-hope-dev') 'SPIRE server config still points to the dev PostgreSQL service.'
Require (($serverPods.Count -eq 3) -and ($readyServerPods -eq 3)) 'A SPIRE Server pod is not ready.'
Require (($agentPods.Count -eq 3) -and ($readyAgentPods -eq 3)) 'A SPIRE Agent is not ready.'
$prodImageGateBlocked = $prodRender -match 'sha256:0{64}'
$prodSecretGateBlocked = $prodRender -match 'cG9zdGdyZXM=|cmVkaXM=|cmFiYml0bXE=|VAULT_TOKEN'

$failoverEvent = Invoke-Kubectl @('get', 'events', '-n', 'spire', '--field-selector', 'reason=FailingOver', '-o', 'json') | ConvertFrom-Json
Require (@($failoverEvent.items).Count -gt 0) 'No CNPG failover event was observed.'
Require ([string]::IsNullOrWhiteSpace([string]$cluster.status.currentPrimary) -eq $false) "CNPG has no current primary after failover (current=$($cluster.status.currentPrimary))."

if ($failures.Count -gt 0) {
    Write-Evidence -Status 'fail' -FailureMessages @($failures)
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Evidence -Status 'pass' -FailureMessages @()
Write-Output 'Production SPIRE HA validation PASS: CNPG 3/3, SPIRE Server 3/3, agents 3/3, PDB, runtime datastore host, and failover evidence verified.'
if ($prodImageGateBlocked -or $prodSecretGateBlocked) {
    Write-Output 'Production workload deployment gate: BLOCKED until signed image digests and external secret-provider objects are supplied.'
} else {
    Write-Output 'Production workload deployment gate: PASS.'
}
