[CmdletBinding()]
param(
    [string]$Namespace = "spire",
    [string]$ServerPod = "spire-server-0",
    [string]$TrustDomain = "his-hope.local",
    [string]$ClusterName = "his-hope-k3s"
)

$ErrorActionPreference = "Stop"
$socket = "/run/spire-server/private/api.sock"
$spireServer = "/opt/spire/bin/spire-server"

function Invoke-SpireServer {
    param([string[]]$Arguments)
    & kubectl exec -n $Namespace $ServerPod -c spire-server -- $spireServer @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "spire-server command failed: $($Arguments -join ' ')"
    }
}

kubectl wait --for=condition=ready pod/$ServerPod -n $Namespace --timeout=180s | Out-Null
$existing = (& kubectl exec -n $Namespace $ServerPod -c spire-server -- $spireServer entry show -socketPath $socket 2>$null) -join "`n"

function Ensure-Entry {
    param(
        [string]$SpiffeId,
        [string[]]$Selectors,
        [switch]$Node,
        [string]$ParentId
    )

    if ($existing -match [regex]::Escape($SpiffeId)) {
        Write-Host "SPIRE entry exists: $SpiffeId"
        return
    }

    $arguments = @("entry", "create", "-socketPath", $socket, "-spiffeID", $SpiffeId)
    foreach ($selector in $Selectors) {
        $arguments += @("-selector", $selector)
    }
    if ($Node) {
        $arguments += "-node"
    } elseif ($ParentId) {
        $arguments += @("-parentID", $ParentId)
    }

    Invoke-SpireServer -Arguments $arguments
    Write-Host "SPIRE entry created: $SpiffeId"
}

$agentId = "spiffe://$TrustDomain/ns/spire/sa/spire-agent"
Ensure-Entry -SpiffeId $agentId -Node -Selectors @(
    "k8s_psat:cluster:$ClusterName",
    "k8s_psat:agent_ns:spire",
    "k8s_psat:agent_sa:spire-agent"
)

foreach ($workloadNamespace in @("his-hope-dev", "his-hope")) {
    foreach ($service in @(
        "identity-service",
        "patient-service",
        "clinical-service",
        "appointment-service",
        "lab-service",
        "billing-service",
        "pharmacy-service"
    )) {
        Ensure-Entry -SpiffeId "spiffe://$TrustDomain/ns/$workloadNamespace/sa/$service" -Selectors @(
            "k8s:ns:$workloadNamespace",
            "k8s:sa:$service"
        ) -ParentId $agentId
    }
}

# Production manifests use prefixed ServiceAccount names (for example
# `his-hope-patient-service`) while the SPIFFE ID remains canonical. Keep a
# second selector entry so the K8s workload attestor can issue the SVID.
foreach ($service in @(
    "identity-service",
    "patient-service",
    "clinical-service",
    "appointment-service",
    "lab-service",
    "billing-service",
    "pharmacy-service"
)) {
    $prefixedSelector = "k8s:sa:his-hope-$service"
    if ($existing -notmatch [regex]::Escape($prefixedSelector)) {
        Invoke-SpireServer -Arguments @(
            "entry", "create", "-socketPath", $socket,
            "-spiffeID", "spiffe://$TrustDomain/ns/his-hope/sa/$service",
            "-selector", "k8s:ns:his-hope",
            "-selector", $prefixedSelector,
            "-parentID", $agentId
        )
        Write-Host "SPIRE prefixed ServiceAccount entry created: $prefixedSelector"
    }
}

Ensure-Entry -SpiffeId "spiffe://$TrustDomain/ns/spire/sa/spire-test" -Selectors @(
    "k8s:ns:spire",
    "k8s:sa:spire-test"
) -ParentId $agentId

Invoke-SpireServer -Arguments @("entry", "show", "-socketPath", $socket)
