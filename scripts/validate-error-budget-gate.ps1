[CmdletBinding()]
param(
    [string]$RuleManifest = 'k8s/observability/k3s-observability.yaml',
    [string]$PrometheusUrl,
    [switch]$RequireLive,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [ValidateSet('pass','fail','blocked','skipped')][string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}

if (-not (Test-Path -LiteralPath $RuleManifest -PathType Leaf)) {
    Add-Check 'static-error-budget-rule' 'fail' "Missing PrometheusRule manifest: $RuleManifest"
} else {
    $text = Get-Content -LiteralPath $RuleManifest -Raw
    $required = @(
        'alert:\s*HisHopeErrorBudgetBurnCritical\b',
        'description:\s*"[^"]*hold production promotion',
        'for:\s*5m',
        '>\s*0\.0144',
        '\[5m\]',
        '\[1h\]'
    )
    $missing = @($required | Where-Object { $text -notmatch $_ })
    if ($missing.Count -gt 0) {
        Add-Check 'static-error-budget-rule' 'fail' "Critical burn-rate rule is incomplete; missing $($missing.Count) contract fragment(s)."
    } else {
        Add-Check 'static-error-budget-rule' 'pass' 'Critical multi-window error-budget rule holds promotion above the 14.4x burn threshold.'
    }
}

if ([string]::IsNullOrWhiteSpace($PrometheusUrl)) {
    if ($RequireLive) {
        Add-Check 'live-error-budget' 'blocked' 'PROMETHEUS_URL is required for the live production error-budget query.'
    } else {
        Add-Check 'live-error-budget' 'skipped' 'Live Prometheus query was not requested.'
    }
} else {
    $query = 'max(((1 - sum(rate(http_server_request_duration_seconds_count{namespace=~"his-hope|his-hope-staging",http_response_status_code!~"5.."}[5m])) / clamp_min(sum(rate(http_server_request_duration_seconds_count{namespace=~"his-hope|his-hope-staging"}[5m])), 1)) > 0.0144) and ((1 - sum(rate(http_server_request_duration_seconds_count{namespace=~"his-hope|his-hope-staging",http_response_status_code!~"5.."}[1h])) / clamp_min(sum(rate(http_server_request_duration_seconds_count{namespace=~"his-hope|his-hope-staging"}[1h])), 1)) > 0.0144))'
    try {
        $base = $PrometheusUrl.TrimEnd('/')
        $encoded = [Uri]::EscapeDataString($query)
        $response = Invoke-RestMethod -Method Get -Uri "$base/api/v1/query?query=$encoded" -TimeoutSec 20
        if ($response.status -ne 'success') { throw 'Prometheus returned a non-success status.' }
        $value = if ($response.data.result.Count -eq 0) { 0.0 } else { [double]$response.data.result[0].value[1] }
        if ($value -gt 0) {
            Add-Check 'live-error-budget' 'fail' "Prometheus reports burn-rate threshold exceeded (query result=$value); promotion must be held."
        } else {
            Add-Check 'live-error-budget' 'pass' 'Prometheus reports no active critical error-budget burn.'
        }
    } catch {
        Add-Check 'live-error-budget' 'blocked' "Unable to query Prometheus without exposing credentials: $($_.Exception.Message)"
    }
}

$hasFail = @($checks | Where-Object status -eq 'fail').Count -gt 0
$hasBlocked = @($checks | Where-Object status -eq 'blocked').Count -gt 0
$status = if ($hasFail) { 'fail' } elseif ($hasBlocked) { 'blocked' } else { 'pass' }
$result = [pscustomobject]@{ status = $status; checks = @($checks); generatedAtUtc = [DateTime]::UtcNow.ToString('o') }
$json = $result | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $dir = Split-Path -Parent $OutputPath
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -eq 'fail') { exit 80 }
if ($status -eq 'blocked') { exit 70 }
exit 0
