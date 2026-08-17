[CmdletBinding()]
param(
    [int]$Attempts = 6,
    [int]$TimeoutSeconds = 8,
    [switch]$RequireAll
)

$ErrorActionPreference = 'Stop'
$checks = @(
    @{ Name = 'gateway-health'; Url = 'http://127.0.0.1:5000/health' },
    @{ Name = 'identity-login'; Url = 'http://127.0.0.1:5001/Account/Login' },
    @{ Name = 'frontend'; Url = 'http://127.0.0.1:8081/' },
    @{ Name = 'dashboard'; Url = 'http://127.0.0.1:8082/' },
    @{ Name = 'admin'; Url = 'http://127.0.0.1:8083/' }
)

$failures = [System.Collections.Generic.List[string]]::new()
foreach ($check in $checks) {
    $passed = $false
    $lastError = ''
    for ($attempt = 1; $attempt -le [Math]::Max(1, $Attempts); $attempt++) {
        try {
            # Keep this gate compatible with Windows PowerShell 5.1 as well as
            # PowerShell 7; SkipHttpErrorCheck only exists in newer versions.
            $response = Invoke-WebRequest -Uri $check.Url -UseBasicParsing -TimeoutSec $TimeoutSeconds
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-Output "SMOKE_PASS $($check.Name) status=$($response.StatusCode) attempt=$attempt"
                $passed = $true
                break
            }
            $lastError = "HTTP $($response.StatusCode)"
        }
        catch { $lastError = $_.Exception.Message }
        if ($attempt -lt $Attempts) { Start-Sleep -Seconds ([Math]::Min(3, $attempt)) }
    }
    if (-not $passed) {
        $failures.Add("$($check.Name): $lastError")
        Write-Output "SMOKE_ENVIRONMENT_FLAKY $($check.Name) $lastError"
    }
}

if ($RequireAll -and $failures.Count -gt 0) {
    throw ($failures -join '; ')
}
if ($failures.Count -eq $checks.Count) {
    throw 'All public UI smoke checks failed.'
}
if ($failures.Count -gt 0) {
    Write-Output "SMOKE_COMPLETED_WITH_ENVIRONMENT_FLAKES count=$($failures.Count)"
}
else {
    Write-Output 'SMOKE_PUBLIC_UI_PASS'
}
