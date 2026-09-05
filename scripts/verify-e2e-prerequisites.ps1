param(
    [string[]] $ServerUrls = @(
        "$(if ($env:E2E_CLINICAL_URL) { $env:E2E_CLINICAL_URL } else { 'http://localhost:8081' })/",
        "$(if ($env:E2E_DASHBOARD_URL) { $env:E2E_DASHBOARD_URL } else { 'http://localhost:8082' })/",
        "$(if ($env:E2E_ADMIN_URL) { $env:E2E_ADMIN_URL } else { 'http://localhost:8083' })/"
    ),
    [string] $AuthProbeUrl = $env:E2E_AUTH_PROBE_URL,
    [string] $AuthToken = $env:E2E_AUTH_TOKEN,
    [string] $AuthEmail = $env:E2E_EMAIL,
    [string] $AuthPassword = $env:E2E_PASSWORD
)

$ErrorActionPreference = 'Stop'
if ($env:E2E_AUTH_REQUIRED -ne 'true') { throw 'Authenticated E2E gate requires E2E_AUTH_REQUIRED=true.' }
if ([string]::IsNullOrWhiteSpace($AuthProbeUrl)) { throw 'Authenticated E2E gate requires E2E_AUTH_PROBE_URL.' }
if ([string]::IsNullOrWhiteSpace($AuthToken)) { throw 'Authenticated E2E gate requires E2E_AUTH_TOKEN.' }
if ([string]::IsNullOrWhiteSpace($AuthEmail) -or [string]::IsNullOrWhiteSpace($AuthPassword)) {
    throw 'Authenticated E2E gate requires E2E_EMAIL and E2E_PASSWORD from protected secret storage.'
}

$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(15)
try {
    foreach ($serverUrl in $ServerUrls) {
        try { $response = $client.GetAsync($serverUrl).GetAwaiter().GetResult() }
        catch { throw "E2E server prerequisite failed for '$serverUrl': $($_.Exception.Message)" }
        if (-not $response.IsSuccessStatusCode) { throw "E2E server prerequisite failed for '$serverUrl': HTTP $([int]$response.StatusCode)." }
        Write-Host "E2E server reachable: $serverUrl"
    }

    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $AuthProbeUrl)
    $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $AuthToken)
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) { throw "Authenticated E2E prerequisite failed for '$AuthProbeUrl': HTTP $([int]$response.StatusCode)." }
    Write-Host 'Authenticated E2E prerequisite passed.'
}
finally { $client.Dispose() }
