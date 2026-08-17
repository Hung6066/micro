[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$E2ePassword,
    [string]$ComposeFile = "docker/docker-compose.yml"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($E2ePassword.Length -lt 12) {
    throw 'E2E password must be at least 12 characters.'
}

$env:E2E_PASSWORD = $E2ePassword
try {
    docker compose -f $ComposeFile -f docker/docker-compose.e2e.yml up -d identityservice
    if ($LASTEXITCODE -ne 0) {
        throw "Identity E2E compose startup failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item Env:E2E_PASSWORD -ErrorAction SilentlyContinue
}
