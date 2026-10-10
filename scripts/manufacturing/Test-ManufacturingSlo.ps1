param(
    [string]$BaseUrl = 'http://localhost:4300',
    [int]$Samples = 10,
    [int]$MaxP95Milliseconds = 1000,
    [int]$MinAvailabilityPercent = 99,
    [string]$AccessToken = $env:HIS_HOPE_MANUFACTURING_ACCESS_TOKEN,
    [string]$SessionCookie = $env:HIS_HOPE_MANUFACTURING_SESSION_COOKIE,
    [Microsoft.PowerShell.Commands.WebRequestSession]$WebSession,
    [switch]$SkipAuthenticated
)

$ErrorActionPreference = 'Stop'
if ($Samples -lt 3) { throw 'Samples must be at least 3.' }
if (-not $SkipAuthenticated -and [string]::IsNullOrWhiteSpace($AccessToken) -and [string]::IsNullOrWhiteSpace($SessionCookie) -and $null -eq $WebSession) {
    throw 'Authenticated SLO probe requires -AccessToken, -SessionCookie, or -WebSession (or the matching environment variables). Use -SkipAuthenticated only for infrastructure-only checks.'
}
$paths = @('/health/ready')
if (-not $SkipAuthenticated) { $paths += '/api/v1/manufacturing/events/receipts?limit=1' }
$headers = @{}
if (-not [string]::IsNullOrWhiteSpace($AccessToken)) { $headers['Authorization'] = "Bearer $AccessToken" }
if (-not [string]::IsNullOrWhiteSpace($SessionCookie)) { $headers['Cookie'] = $SessionCookie }
$observations = [System.Collections.Generic.List[object]]::new()
foreach ($path in $paths) {
    1..$Samples | ForEach-Object {
        $watch = [Diagnostics.Stopwatch]::StartNew()
        try {
            $request = @{ Uri = ($BaseUrl.TrimEnd('/') + $path); UseBasicParsing = $true; TimeoutSec = 10 }
            if ($headers.Count -gt 0) { $request.Headers = $headers }
            if ($null -ne $WebSession) { $request.WebSession = $WebSession }
            $response = Invoke-WebRequest @request
            $status = [int]$response.StatusCode
        } catch {
            $status = 0
        } finally { $watch.Stop() }
        $observations.Add([pscustomobject]@{ Path = $path; Status = $status; LatencyMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 2) })
    }
}

$availability = [math]::Round((($observations | Where-Object { $_.Status -ge 200 -and $_.Status -lt 400 }).Count / $observations.Count) * 100, 2)
$sorted = @($observations.LatencyMs | Sort-Object)
$p95 = $sorted[[math]::Min($sorted.Count - 1, [math]::Ceiling($sorted.Count * .95) - 1)]
$result = [pscustomobject]@{
    BaseUrl = $BaseUrl
    Samples = $observations.Count
    AvailabilityPercent = $availability
    P95LatencyMilliseconds = $p95
    AvailabilityPass = $availability -ge $MinAvailabilityPercent
    LatencyPass = $p95 -le $MaxP95Milliseconds
    AuthenticatedProbeConfigured = $SkipAuthenticated -or -not [string]::IsNullOrWhiteSpace($AccessToken) -or -not [string]::IsNullOrWhiteSpace($SessionCookie) -or $null -ne $WebSession
    AuthenticatedProbeAuthMode = if ($SkipAuthenticated) { 'skipped' } elseif (-not [string]::IsNullOrWhiteSpace($AccessToken)) { 'bearer' } elseif ($null -ne $WebSession) { 'web-session' } else { 'session-cookie' }
    AuthenticatedProbePass = $SkipAuthenticated -or (($observations | Where-Object { $_.Path -like '/api/v1/manufacturing/events/receipts*' -and $_.Status -ge 200 -and $_.Status -lt 400 }).Count -eq $Samples)
    MeasuredAtUtc = (Get-Date).ToUniversalTime().ToString('o')
}
$result | ConvertTo-Json
if (-not $result.AvailabilityPass -or -not $result.LatencyPass -or -not $result.AuthenticatedProbePass) { exit 2 }
