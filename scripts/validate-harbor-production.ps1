[CmdletBinding()]
param(
    [string]$HostName = 'harbor.his-hope.local',
    [int]$HttpsPort = 9443,
    [string]$CaFile = 'D:\secure\his-hope\his_hope_ca.pem',
    [string]$CanaryDigest = 'sha256:badc9b3ca143f3063057711c986a3002d16bd9ac8fc153f08346f22ac59a86e3',
    [string]$CosignPath
)

$ErrorActionPreference = 'Stop'
$baseUrl = "https://$HostName`:$HttpsPort"
$health = curl.exe --silent --show-error --ssl-no-revoke --cacert $CaFile --resolve "$HostName`:$HttpsPort`:127.0.0.1" "$baseUrl/api/v2.0/health"
if ($LASTEXITCODE -ne 0) { throw 'Harbor HTTPS health request failed' }
$healthObject = $health | ConvertFrom-Json
if ($healthObject.status -ne 'healthy') { throw "Harbor status is $($healthObject.status)" }

$release = helm status harbor --namespace harbor --output json | ConvertFrom-Json
if ($release.info.status -ne 'deployed') { throw "Harbor Helm release status is $($release.info.status)" }
$pending = @(kubectl get pvc -n harbor -o json | ConvertFrom-Json).items | Where-Object { $_.status.phase -ne 'Bound' }
if ($pending.Count -gt 0) { throw "Harbor PVCs are not Bound: $($pending.metadata.name -join ', ')" }
$notReady = @(kubectl get pods -n harbor -o json | ConvertFrom-Json).items | Where-Object {
    $_.status.phase -ne 'Running' -or (@($_.status.containerStatuses) | Where-Object { -not $_.ready }).Count -gt 0
}
if ($notReady.Count -gt 0) { throw "Harbor pods are not ready: $($notReady.metadata.name -join ', ')" }

$cosign = if (-not [string]::IsNullOrWhiteSpace($CosignPath)) {
    if (-not (Test-Path $CosignPath)) { throw "Cosign binary not found: $CosignPath" }
    [pscustomobject]@{ Source = (Resolve-Path $CosignPath).Path }
} else { Get-Command cosign -ErrorAction SilentlyContinue }
if ($cosign) {
    $ref = "$HostName`:$HttpsPort/his-hope/identity-service@$CanaryDigest"
    & $cosign.Source verify --key D:\secure\his-hope\cosign.pub $ref 1>$null
    if ($LASTEXITCODE -ne 0) { throw 'Harbor canary Cosign verification failed' }
}
Write-Output "Harbor production gate PASS: HTTPS healthy, Helm deployed, PVCs Bound, pods Ready, canary signature verified when cosign is available."
