[CmdletBinding()]
param(
    [ValidateSet('staging', 'production')][string]$Environment = 'staging',
    [string]$Kubeconfig = 'artifacts/kubeconfig-staging.yaml',
    [string]$Namespace = 'his-hope',
    [Parameter(Mandatory)][ValidatePattern('@sha256:[0-9a-f]{64}$')][string]$SignedImage,
    [Parameter(Mandatory)][ValidatePattern('@sha256:[0-9a-f]{64}$')][string]$UnsignedImage,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Environment -eq 'production' -and -not $AllowProduction) {
    throw 'Production signature-admission test is protected; use the approved workflow.'
}
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }
foreach ($image in @($SignedImage, $UnsignedImage)) {
    if ($image -notmatch '^harbor\.[^/]+/his-hope/[a-z0-9][a-z0-9./-]*@sha256:[0-9a-f]{64}$') {
        throw 'Both images must be approved Harbor his-hope digest references.'
    }
}

$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
function Invoke-DryRun([string]$Image, [string]$Name) {
    $manifest = @"
apiVersion: v1
kind: Pod
metadata:
  generateName: $Name-
  namespace: $Namespace
  labels:
    app.kubernetes.io/name: signature-admission-test
spec:
  restartPolicy: Never
  automountServiceAccountToken: false
  securityContext:
    runAsNonRoot: true
    seccompProfile:
      type: RuntimeDefault
  containers:
    - name: probe
      image: $Image
      command: ["/bin/sh", "-c"]
      args: ["exit 0"]
      securityContext:
        allowPrivilegeEscalation: false
        capabilities:
          drop: [ALL]
"@
    $output = $manifest | & kubectl apply --dry-run=server -f - 2>&1
    return [pscustomobject]@{ Accepted = ($LASTEXITCODE -eq 0); Output = ($output -join "`n") }
}

$signed = Invoke-DryRun $SignedImage 'signed-admission'
if (-not $signed.Accepted) { throw 'Signed-image admission negative result: the approved signed image was rejected.' }

$unsigned = Invoke-DryRun $UnsignedImage 'unsigned-admission'
if ($unsigned.Accepted) { throw 'Unsigned-image admission negative test failed: an unsigned image was accepted.' }
Write-Output 'Signature admission PASS: signed digest accepted and unsigned digest rejected by server-side dry-run.'
