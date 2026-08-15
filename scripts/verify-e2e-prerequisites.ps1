param(
    [string[]] $ServerUrls = @(
        "$(if ($env:E2E_CLINICAL_URL) { $env:E2E_CLINICAL_URL } else { 'http://localhost:8081' })/",
        "$(if ($env:E2E_DASHBOARD_URL) { $env:E2E_DASHBOARD_URL } else { 'http://localhost:8082' })/",
        "$(if ($env:E2E_ADMIN_URL) { $env:E2E_ADMIN_URL } else { 'http://localhost:8083' })/"
    ),
    [string] $AuthProbeUrl = $env:E2E_AUTH_PROBE_URL,
    [string] $AuthToken = $env:E2E_AUTH_TOKEN,
    [int] $TimeoutSeconds = $(if ($env:E2E_AUTH_TIMEOUT_SECONDS) { [int]$env:E2E_AUTH_TIMEOUT_SECONDS } else { 60 })
)

$ErrorActionPreference = 'Stop'
if ($env:E2E_AUTH_REQUIRED -ne 'true') { throw 'Authenticated E2E gate requires E2E_AUTH_REQUIRED=true.' }
if ([string]::IsNullOrWhiteSpace($AuthProbeUrl)) { throw 'Authenticated E2E gate requires E2E_AUTH_PROBE_URL.' }
if ([string]::IsNullOrWhiteSpace($AuthToken)) { throw 'Authenticated E2E gate requires E2E_AUTH_TOKEN.' }
if ($TimeoutSeconds -lt 5 -or $TimeoutSeconds -gt 120) { throw 'E2E_AUTH_TIMEOUT_SECONDS must be between 5 and 120.' }

$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
try {
    foreach ($serverUrl in $ServerUrls) {
        try { $response = $client.GetAsync($serverUrl).GetAwaiter().GetResult() }
        catch { throw "E2E server prerequisite failed for '$serverUrl': $($_.Exception.Message)" }
        if (-not $response.IsSuccessStatusCode) { throw "E2E server prerequisite failed for '$serverUrl': HTTP $([int]$response.StatusCode)." }
        Write-Host "E2E server reachable: $serverUrl"
    }

    $probeUri = [Uri]$AuthProbeUrl
    Write-Host "Authenticated probe target: $($probeUri.Scheme)://$($probeUri.Host)$($probeUri.AbsolutePath)"
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $probeUri)
    $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $AuthToken)
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) { throw "Authenticated E2E prerequisite failed for '$AuthProbeUrl': HTTP $([int]$response.StatusCode)." }
    Write-Host 'Authenticated E2E prerequisite passed.'
}
finally { $client.Dispose() }
