[CmdletBinding()]
param(
    [string]$ComposeFile = "docker/docker-compose.yml"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($env:E2E_PASSWORD)) {
    throw 'E2E_PASSWORD must be supplied by protected process environment; never pass credentials as command-line arguments.'
}

if ($env:E2E_PASSWORD.Length -lt 12) {
    throw 'E2E password must be at least 12 characters.'
}

docker compose -f $ComposeFile -f docker/docker-compose.e2e.yml up -d identityservice
if ($LASTEXITCODE -ne 0) {
    throw "Identity E2E compose startup failed with exit code $LASTEXITCODE."
}

    # The compose healthcheck only verifies that the .NET runtime exists, so
    # the container can report healthy before Kestrel has bound its HTTP port.
    # Wait for the externally published readiness endpoint before returning;
    # otherwise the browser proxy can observe a transient 502 during SSO.

$deadline = (Get-Date).AddMinutes(2)
$ready = $false
while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -Uri 'http://localhost:5001/health' -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            $ready = $true
            break
        }
    } catch {
        # Kestrel is still starting; retry until the bounded deadline.
    }
    Start-Sleep -Seconds 2
}
if (-not $ready) {
    throw 'Identity E2E startup timed out waiting for http://localhost:5001/health.'
}
