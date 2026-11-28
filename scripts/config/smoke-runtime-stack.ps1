[CmdletBinding()]
param(
    [string]$ApiOrigin = 'http://localhost:5000',
    [string]$DashboardOrigin = 'http://localhost:5700',
    [switch]$RequireAll
)
$ErrorActionPreference = 'Stop'
$checks = @(
    @{ Name='gateway'; Url="$ApiOrigin/health" },
    @{ Name='oidc-discovery'; Url="$ApiOrigin/.well-known/openid-configuration" },
    @{ Name='dashboard-bff'; Url="$DashboardOrigin/health" }
)
$failures = @()
foreach ($check in $checks) {
    try {
        # Keep this compatible with Windows PowerShell 5.1 as well as pwsh.
        # These smoke checks expect successful responses, so terminating on
        # non-success is sufficient and avoids SkipHttpErrorCheck (pwsh-only).
        $response = Invoke-WebRequest -Uri $check.Url -Method Get -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) { throw "HTTP $($response.StatusCode)" }
        Write-Output ("SMOKE_PASS {0} status={1}" -f $check.Name, $response.StatusCode)
    } catch {
        $failures += "$($check.Name): $($_.Exception.Message)"
        Write-Output ("SMOKE_FAIL {0} {1}" -f $check.Name, $_.Exception.Message)
    }
}
if ($RequireAll -and $failures.Count -gt 0) { throw ($failures -join '; ') }
if ($failures.Count -eq $checks.Count) { throw 'All runtime smoke checks failed.' }
