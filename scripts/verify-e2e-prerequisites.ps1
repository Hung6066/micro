param(
    [string[]] $ServerUrls = @(
        "$(if ($env:E2E_CLINICAL_URL) { $env:E2E_CLINICAL_URL } else { 'http://localhost:8081' })/",
        "$(if ($env:E2E_DASHBOARD_URL) { $env:E2E_DASHBOARD_URL } else { 'http://localhost:8082' })/",
        "$(if ($env:E2E_ADMIN_URL) { $env:E2E_ADMIN_URL } else { 'http://localhost:8083' })/"
    ),
    [string] $AuthProbeUrl = $env:E2E_AUTH_PROBE_URL,
    [string] $AuthToken = $env:E2E_AUTH_TOKEN,
    [int] $TimeoutSeconds = $(if ($env:E2E_AUTH_TIMEOUT_SECONDS) { [int]$env:E2E_AUTH_TIMEOUT_SECONDS } else { 60 }),
    [int] $MaxAttempts = $(if ($env:E2E_AUTH_MAX_ATTEMPTS) { [int]$env:E2E_AUTH_MAX_ATTEMPTS } else { 5 }),
    [int] $RetryDelaySeconds = $(if ($env:E2E_AUTH_RETRY_DELAY_SECONDS) { [int]$env:E2E_AUTH_RETRY_DELAY_SECONDS } else { 10 })
)

$ErrorActionPreference = 'Stop'
if ($env:E2E_AUTH_REQUIRED -ne 'true') { throw 'Authenticated E2E gate requires E2E_AUTH_REQUIRED=true.' }
if ([string]::IsNullOrWhiteSpace($AuthProbeUrl)) { throw 'Authenticated E2E gate requires E2E_AUTH_PROBE_URL.' }
if ([string]::IsNullOrWhiteSpace($AuthToken)) { throw 'Authenticated E2E gate requires E2E_AUTH_TOKEN.' }
if ($TimeoutSeconds -lt 5 -or $TimeoutSeconds -gt 120) { throw 'E2E_AUTH_TIMEOUT_SECONDS must be between 5 and 120.' }
if ($MaxAttempts -lt 1 -or $MaxAttempts -gt 12) { throw 'E2E_AUTH_MAX_ATTEMPTS must be between 1 and 12.' }
if ($RetryDelaySeconds -lt 1 -or $RetryDelaySeconds -gt 60) { throw 'E2E_AUTH_RETRY_DELAY_SECONDS must be between 1 and 60.' }

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
    # Keep the protected URL useful for runtime diagnosis without allowing
    # query strings, bearer material, or the secret's full value into logs.
    Write-Host "Authenticated probe scheme: $($probeUri.Scheme)"
    Write-Host "Authenticated probe host: $($probeUri.Host)"
    Write-Host "Authenticated probe path: $($probeUri.AbsolutePath)"
    $lastFailure = $null
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $probeUri)
        $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $AuthToken)
        try {
            $response = $client.SendAsync($request).GetAwaiter().GetResult()
            if ($response.IsSuccessStatusCode) {
                Write-Host "Authenticated E2E prerequisite passed (attempt $attempt/$MaxAttempts)."
                return
            }

            $statusCode = [int]$response.StatusCode
            $lastFailure = "HTTP $statusCode"
            # A transient gateway response is retried while a bad token or
            # application-level 4xx remains fail-closed immediately.
            if ($statusCode -lt 500 -or $statusCode -gt 504 -or $attempt -eq $MaxAttempts) { break }
        }
        catch {
            $lastFailure = $_.Exception.Message
            if ($attempt -eq $MaxAttempts) { break }
        }

        Write-Host "Authenticated probe transient failure ($lastFailure); retrying in $RetryDelaySeconds seconds ($attempt/$MaxAttempts)."
        Start-Sleep -Seconds $RetryDelaySeconds
    }

    throw "Authenticated E2E prerequisite failed for '$AuthProbeUrl' after $MaxAttempts attempt(s): $lastFailure."
}
finally { $client.Dispose() }
