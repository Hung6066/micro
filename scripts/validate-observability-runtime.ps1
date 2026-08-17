[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Kubeconfig,
    [string]$Namespace = 'monitoring',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }
$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}

$podDocument = kubectl get pods -n $Namespace -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw "Unable to inspect namespace $Namespace pods." }
$badPods = @($podDocument.items | Where-Object {
    $_.status.phase -notin @('Running', 'Succeeded') -or
    @($_.status.containerStatuses | Where-Object { $_.ready -ne $true }).Count -gt 0
})
if ($badPods.Count -eq 0) {
    Add-Check 'monitoring-pods' 'pass' "$($podDocument.items.Count) monitoring pod(s) are ready or completed."
} else {
    $names = ($badPods | ForEach-Object { $_.metadata.name }) -join ', '
    Add-Check 'monitoring-pods' 'fail' "Unhealthy monitoring pods: $names"
}

$hpaDocument = kubectl get hpa -n $Namespace -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw "Unable to inspect namespace $Namespace HPAs." }
$unknownHpa = @($hpaDocument.items | Where-Object {
    @($_.status.currentMetrics).Count -eq 0 -or
    [string]$_.status.currentReplicas -eq ''
})
if ($unknownHpa.Count -eq 0) {
    Add-Check 'monitoring-hpa-metrics' 'pass' "$($hpaDocument.items.Count) monitoring HPA(s) have current metrics."
} else {
    $names = ($unknownHpa | ForEach-Object { $_.metadata.name }) -join ', '
    Add-Check 'monitoring-hpa-metrics' 'fail' "Monitoring HPA metrics are unavailable: $names"
}

$failed = @($checks | Where-Object status -eq 'fail')
$status = if ($failed.Count -eq 0) { 'pass' } else { 'fail' }
$result = [pscustomobject]@{ status = $status; checks = @($checks); generatedAtUtc = [DateTime]::UtcNow.ToString('o') }
$json = $result | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $dir = Split-Path -Parent $OutputPath
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -eq 'fail') { exit 30 }
