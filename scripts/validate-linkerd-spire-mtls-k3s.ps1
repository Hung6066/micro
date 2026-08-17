[CmdletBinding()]
param(
    [string]$Namespace = 'his-hope-dev',
    [string]$NetworkPolicyName = 'allow-linkerd-backend-mesh',
    [string]$Kubeconfig = 'artifacts/kubeconfig-production.yaml'
)

$ErrorActionPreference = 'Stop'

if (Test-Path -LiteralPath $Kubeconfig -PathType Leaf) {
    $env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
}

function Invoke-Kubectl {
    param([string[]]$Arguments)
    $output = & kubectl @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "kubectl $($Arguments -join ' ') failed: $($output -join ' ')"
    }
    return $output
}

$policy = $null
foreach ($candidate in @($NetworkPolicyName, "$Namespace-$NetworkPolicyName", 'his-hope-allow-linkerd-backend-mesh')) {
    $candidateOutput = & kubectl get netpol $candidate -n $Namespace -o yaml 2>$null
    if ($LASTEXITCODE -eq 0) { $policy = ($candidateOutput -join "`n"); break }
}
if ([string]::IsNullOrWhiteSpace($policy)) {
    throw "Linkerd backend NetworkPolicy '$NetworkPolicyName' was not found in namespace '$Namespace'."
}
if ($policy -notmatch 'port: 4140' -or $policy -notmatch 'port: 4143') {
    throw 'Linkerd mesh NetworkPolicy must allow egress 4140 and ingress 4143.'
}
Write-Output 'NetworkPolicy: PASS (4140 egress / 4143 ingress)'

$identityPod = (Invoke-Kubectl @('get', 'pods', '-n', $Namespace, '-l', 'app=identity-service', '--field-selector=status.phase=Running', '-o', 'custom-columns=NAME:.metadata.name', '--no-headers') | Select-Object -First 1).Trim()
if ([string]::IsNullOrWhiteSpace($identityPod)) {
    throw 'No running identity-service pod found.'
}

# Record init/container reasons before the traffic probe. A Completed
# linkerd-init is expected; any waiting/terminated failure reason is a
# deployment defect and must remain visible in the gate output.
$controlPlane = (Invoke-Kubectl @('get', 'pods', '-n', 'linkerd', '-o', 'json') | ConvertFrom-Json).items
foreach ($pod in $controlPlane) {
    foreach ($init in @($pod.status.initContainerStatuses)) {
        $state = $init.state | ConvertTo-Json -Compress
        Write-Output "init-reason|$($pod.metadata.name)|$($init.name)|$state"
        if ($init.state.waiting -and $init.state.waiting.reason) { throw "Linkerd init container $($pod.metadata.name)/$($init.name) is waiting: $($init.state.waiting.reason)." }
        if ($init.state.terminated -and $init.state.terminated.exitCode -ne 0) { throw "Linkerd init container $($pod.metadata.name)/$($init.name) failed with exit code $($init.state.terminated.exitCode)." }
    }
}
$identityContainers = (Invoke-Kubectl @('get', 'pod', $identityPod, '-n', $Namespace, '-o', 'json') | ConvertFrom-Json).spec.containers.name
if ($identityContainers -notcontains 'linkerd-proxy') {
    throw "Injector canary failed: $identityPod has no linkerd-proxy container."
}
Write-Output "Injector canary: PASS ($identityPod has linkerd-proxy)"

$targets = @(
    @{ Name = 'patient-service'; Port = 5002 },
    @{ Name = 'appointment-service'; Port = 5004 },
    @{ Name = 'clinical-service'; Port = 5005 },
    @{ Name = 'lab-service'; Port = 5010 },
    @{ Name = 'billing-service'; Port = 5020 },
    @{ Name = 'pharmacy-service'; Port = 5030 }
)

foreach ($target in $targets) {
    $serviceName = $target.Name
    $prefixedName = "$Namespace-$($target.Name)"
    $null = & kubectl get service $serviceName -n $Namespace -o name 2>$null
    if ($LASTEXITCODE -ne 0) {
        $null = & kubectl get service $prefixedName -n $Namespace -o name 2>$null
        if ($LASTEXITCODE -eq 0) { $serviceName = $prefixedName }
    }
    # Use the process-liveness endpoint so this probe tests Linkerd transport
    # and identity policy, not downstream database/cache health aggregation.
    $url = "http://$serviceName`:$($target.Port)/health/live"
    $result = (Invoke-Kubectl @('exec', '-n', $Namespace, $identityPod, '-c', 'identity-service', '--', 'sh', '-c', "curl -sS -m 8 -o /dev/null -w '%{http_code}' $url")).Trim()
    if ($result -ne '200') {
        throw "$($target.Name) mTLS smoke returned HTTP $result."
    }
    Write-Output "$($target.Name): PASS (HTTP 200 through Linkerd)"
}

$portForward = Start-Process kubectl -ArgumentList @('port-forward', '-n', $Namespace, "pod/$identityPod", '4191:4191') -PassThru -WindowStyle Hidden
try {
    Start-Sleep -Seconds 2
    $metrics = (Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:4191/metrics' -TimeoutSec 10).Content
    if ($metrics -notmatch 'request_total\{[^\r\n]*tls="true"') {
        throw 'Linkerd proxy metrics did not contain a TLS-authenticated outbound request.'
    }
    Write-Output 'Linkerd proxy metrics: PASS (tls="true" outbound request observed)'
}
finally {
    Stop-Process -Id $portForward.Id -Force -ErrorAction SilentlyContinue
}

Write-Output 'Linkerd + SPIRE mTLS validation: PASS'
