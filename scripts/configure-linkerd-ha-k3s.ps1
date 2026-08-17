[CmdletBinding()]
param(
    [string]$Namespace = 'linkerd',
    [int]$Replicas = 3,
    [switch]$Failover
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Replicas -lt 2) { throw 'HA requires at least two replicas.' }
$deployments = @(
    @{ Name = 'linkerd-destination'; Component = 'destination'; Services = @('linkerd-dst', 'linkerd-dst-headless') },
    @{ Name = 'linkerd-identity'; Component = 'identity'; Services = @('linkerd-identity', 'linkerd-identity-headless') },
    @{ Name = 'linkerd-proxy-injector'; Component = 'proxy-injector'; Services = @('linkerd-proxy-injector') }
)

$nodes = @(kubectl get nodes -o json | ConvertFrom-Json).items
if ($nodes.Count -lt $Replicas) {
    throw "Linkerd HA needs at least $Replicas schedulable nodes; found $($nodes.Count)."
}

kubectl apply -f 'D:\AI\micro\k8s\linkerd\linkerd-ha-pdb.yaml' | Out-Null

foreach ($item in $deployments) {
    $patch = @{
        spec = @{
            replicas = $Replicas
            strategy = @{
                type = 'RollingUpdate'
                rollingUpdate = @{ maxUnavailable = 1; maxSurge = 1 }
            }
            template = @{
                metadata = @{
                    # Linkerd control-plane pods must not be meshed themselves.
                    # Injecting the proxy intercepts policy gRPC (8090) and can
                    # deadlock every workload proxy's policy stream.
                    annotations = @{ 'linkerd.io/inject' = 'disabled' }
                }
                spec = @{
                    affinity = @{
                        podAntiAffinity = @{
                            requiredDuringSchedulingIgnoredDuringExecution = @(
                                @{
                                    labelSelector = @{ matchLabels = @{ 'linkerd.io/control-plane-component' = $item.Component } }
                                    topologyKey = 'kubernetes.io/hostname'
                                }
                            )
                        }
                    }
                    topologySpreadConstraints = @(
                        @{
                            maxSkew = 1
                            topologyKey = 'kubernetes.io/hostname'
                            whenUnsatisfiable = 'DoNotSchedule'
                            labelSelector = @{ matchLabels = @{ 'linkerd.io/control-plane-component' = $item.Component } }
                        }
                    )
                }
            }
        }
    } | ConvertTo-Json -Depth 12 -Compress

    $patchFile = [IO.Path]::GetTempFileName()
    try {
        Set-Content -LiteralPath $patchFile -Value $patch -Encoding utf8
        kubectl patch deployment $item.Name -n $Namespace --type=merge --patch-file $patchFile | Out-Null
    } finally {
        Remove-Item -LiteralPath $patchFile -Force -ErrorAction SilentlyContinue
    }
    if ($LASTEXITCODE -ne 0) { throw "Unable to patch $($item.Name)" }
    kubectl rollout status deployment/$($item.Name) -n $Namespace --timeout=180s
    if ($LASTEXITCODE -ne 0) { throw "Rollout failed for $($item.Name)" }
}

function Get-ReadyPods([string]$DeploymentName) {
    $definition = $deployments | Where-Object { $_['Name'] -eq $DeploymentName }
    $selector = "linkerd.io/control-plane-component=$($definition['Component'])"
    return @((kubectl get pods -n $Namespace -l $selector -o json | ConvertFrom-Json).items | Where-Object {
        $_.status.phase -eq 'Running' -and @($_.status.conditions | Where-Object { $_.type -eq 'Ready' -and $_.status -eq 'True' }).Count -gt 0
    })
}

foreach ($item in $deployments) {
    $pods = @(Get-ReadyPods $item.Name)
    if ($pods.Count -ne $Replicas) { throw "$($item.Name) has $($pods.Count)/$Replicas Ready pods" }
    $nodeCount = @($pods | Select-Object -ExpandProperty spec | Select-Object -ExpandProperty nodeName -Unique).Count
    if ($nodeCount -lt $Replicas) { throw "$($item.Name) is not spread across $Replicas nodes" }
    foreach ($service in $item.Services) {
        $endpointCount = @((kubectl get endpointslice -n $Namespace -l "kubernetes.io/service-name=$service" -o json | ConvertFrom-Json).items | ForEach-Object { $_.endpoints } | Where-Object { $_.conditions.ready -ne $false }).Count
        if ($endpointCount -lt 2) { throw "$service has fewer than two ready endpoints" }
    }
}

if ($Failover) {
    foreach ($item in $deployments) {
        $pod = @(Get-ReadyPods $item.Name | Select-Object -First 1)
        if ($pod.Count -ne 1) { throw "No failover target found for $($item.Name)" }
        kubectl delete pod $pod[0].metadata.name -n $Namespace --wait=true | Out-Null
        kubectl rollout status deployment/$($item.Name) -n $Namespace --timeout=180s
        $replacement = @(Get-ReadyPods $item.Name)
        if ($replacement.Count -ne $Replicas) { throw "$($item.Name) did not recover to $Replicas Ready pods" }
        foreach ($service in $item.Services) {
            $endpointCount = @((kubectl get endpointslice -n $Namespace -l "kubernetes.io/service-name=$service" -o json | ConvertFrom-Json).items | ForEach-Object { $_.endpoints } | Where-Object { $_.conditions.ready -ne $false }).Count
            if ($endpointCount -lt 2) { throw "$service lost HA endpoint capacity during failover" }
        }
    }
}

Write-Output "Linkerd control-plane HA PASS: $Replicas replicas per deployment, topology-spread, PDB protected, endpoints healthy, failover=$Failover."
