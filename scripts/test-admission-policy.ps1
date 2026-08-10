[CmdletBinding()]
param(
    [string]$PolicyPath = 'k8s/security/gatekeeper-production-constraints.yaml',
    [string]$Kubeconfig = 'artifacts/kubeconfig-staging.yaml',
    [string]$Namespace = 'his-hope',
    [switch]$RequireCluster,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [ValidateSet('pass','fail','skipped','environment-blocked')][string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}

$policy = Get-Content -LiteralPath $PolicyPath -Raw
$requiredPolicyPatterns = @(
    'enforcementAction:\s*deny',
    '@sha256:\[0-9a-f\]\{64\}',
    'hostPath volumes are not allowed',
    'service account token automount must be disabled',
    'drop all Linux capabilities'
)
$missing = @($requiredPolicyPatterns | Where-Object { $policy -notmatch $_ })
if ($missing.Count -gt 0) { Add-Check 'policy-contract' 'fail' "Required policy rules missing: $($missing -join ', ')" }
else { Add-Check 'policy-contract' 'pass' 'Digest, restricted security, hostPath, token and capability rules are present.' }

$positive = @'
apiVersion: v1
kind: Pod
metadata:
  name: admission-positive
  namespace: his-hope
spec:
  automountServiceAccountToken: false
  restartPolicy: Never
  containers:
    - name: app
      image: harbor.his-hope.local:9443/his-hope/example@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
      securityContext:
        runAsNonRoot: true
        allowPrivilegeEscalation: false
        capabilities:
          drop: [ALL]
        seccompProfile:
          type: RuntimeDefault
      resources:
        requests: {cpu: 10m, memory: 16Mi}
        limits: {cpu: 100m, memory: 64Mi}
'@
$negative = @'
apiVersion: v1
kind: Pod
metadata:
  name: admission-negative
  namespace: his-hope
spec:
  automountServiceAccountToken: true
  hostNetwork: true
  restartPolicy: Never
  volumes:
    - name: host
      hostPath: {path: /etc}
  containers:
    - name: app
      image: docker.io/library/example:latest
      securityContext:
        privileged: true
        runAsNonRoot: false
        allowPrivilegeEscalation: true
        seccompProfile:
          type: Unconfined
      resources: {}
'@
$tempDirectory = Join-Path ([IO.Path]::GetTempPath()) "his-hope-admission-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $tempDirectory -Force | Out-Null
$positivePath = Join-Path $tempDirectory 'positive.yaml'
$negativePath = Join-Path $tempDirectory 'negative.yaml'
[IO.File]::WriteAllText($positivePath, $positive, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($negativePath, $negative, [Text.UTF8Encoding]::new($false))
try {
    if (-not $RequireCluster) {
        Add-Check 'cluster-positive-negative' 'skipped' 'Cluster admission tests require a staging kubeconfig and are not inferred from source inspection.'
    } elseif (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) {
        Add-Check 'cluster-positive-negative' 'environment-blocked' "Kubeconfig not found: $Kubeconfig"
    } else {
        $env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
        $positiveOutput = & kubectl apply --server-side --dry-run=server -f $positivePath 2>&1
        if ($LASTEXITCODE -eq 0) { Add-Check 'positive-admission' 'pass' 'Compliant Pod accepted by the API admission chain.' }
        else { Add-Check 'positive-admission' 'fail' 'Compliant Pod was rejected by the API admission chain.' }

        $negativeOutput = & kubectl apply --server-side --dry-run=server -f $negativePath 2>&1
        if ($LASTEXITCODE -ne 0) { Add-Check 'negative-admission' 'pass' 'Privileged/mutable-tag/hostPath Pod was rejected by the API admission chain.' }
        else { Add-Check 'negative-admission' 'fail' 'Unsafe Pod was accepted; fail-closed admission is not active.' }
    }
} finally {
    Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
}

$failed = @($checks | Where-Object status -eq 'fail')
$blocked = @($checks | Where-Object status -eq 'environment-blocked')
$status = if ($failed.Count -gt 0) { 'fail' } elseif ($blocked.Count -gt 0) { 'environment-blocked' } elseif (@($checks | Where-Object status -eq 'skipped').Count -gt 0) { 'skipped' } else { 'pass' }
$result = [pscustomobject]@{ status = $status; checks = @($checks); generatedAtUtc = [DateTime]::UtcNow.ToString('o') }
$json = $result | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $dir = Split-Path -Parent $OutputPath
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -eq 'fail') { exit 30 }
if ($status -in @('environment-blocked','skipped')) { exit 70 }
exit 0
