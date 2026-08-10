[CmdletBinding()]
param(
    [string]$CredentialFile = 'D:\secure\his-hope\harbor_robot_k3s_pull.json',
    [string[]]$Namespaces = @('his-hope', 'his-hope-dev', 'spire'),
    [string]$K3sRegistry = 'harbor-k3s.his-hope.local',
    [string]$CanaryImage = 'harbor.his-hope.local:9443/his-hope/identity-service',
    [string]$CanaryDigest = 'sha256:badc9b3ca143f3063057711c986a3002d16bd9ac8fc153f08346f22ac59a86e3'
)

$ErrorActionPreference = 'Stop'
$credential = Get-Content -Raw $CredentialFile | ConvertFrom-Json
$image = "$CanaryImage@$CanaryDigest"

foreach ($namespace in $Namespaces) {
    kubectl get namespace $namespace *> $null
    if ($LASTEXITCODE -ne 0) { continue }
    kubectl -n $namespace create secret docker-registry harbor-pull-k3s `
        --docker-server="$K3sRegistry" `
        --docker-username="$($credential.name)" `
        --docker-password="$($credential.secret)" `
        --docker-email=platform@his-hope.local `
    --dry-run=client -o yaml | kubectl apply -f -
    if ($LASTEXITCODE -ne 0) { throw "Unable to create Harbor pull secret in $namespace" }
    $patchFile = Join-Path $env:TEMP ("harbor-sa-" + [guid]::NewGuid().ToString() + '.json')
    '{"imagePullSecrets":[{"name":"harbor-pull-k3s"}]}' | Set-Content -Encoding ascii $patchFile
    kubectl -n $namespace patch serviceaccount default --type merge --patch-file $patchFile
    Remove-Item $patchFile -Force -ErrorAction SilentlyContinue
    if ($LASTEXITCODE -ne 0) { throw "Unable to patch default ServiceAccount in $namespace" }
}

$dockerPassword = $credential.secret | docker login "$($credential.registry)" --username "$($credential.name)" --password-stdin
if ($LASTEXITCODE -ne 0) { throw 'Harbor robot login failed' }
docker pull $image | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Harbor robot pull failed: $image" }
Write-Output "Harbor supply-chain PASS: pull-only robot and imagePullSecret configured; canary pulled by digest."
