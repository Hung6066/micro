param(
    [string]$ComposeFile = "$PSScriptRoot\..\docker\docker-compose.yml",
    [string]$EnvFile = "$PSScriptRoot\..\docker\config\compose.runtime.env"
)

$ErrorActionPreference = "Stop"

function Resolve-Docker {
    $candidates = @(
        (Get-Command docker -ErrorAction SilentlyContinue)?.Source,
        "$env:ProgramFiles\Docker\Docker\resources\bin\docker.exe",
        "${env:ProgramFiles(x86)}\Docker\Docker\resources\bin\docker.exe"
    ) | Where-Object { $_ -and (Test-Path $_) }
    return $candidates | Select-Object -First 1
}

$docker = Resolve-Docker
if (-not $docker) {
    throw "Docker CLI not found. Install Docker Desktop and ensure 'docker' is on PATH."
}

Write-Host "Rebuilding identity + gateway + admin containers with updated device posture policy..."
& $docker compose -f $ComposeFile --env-file $EnvFile build identityservice api-gateway admin-app
& $docker compose -f $ComposeFile --env-file $EnvFile up -d --force-recreate identityservice api-gateway admin-app

Write-Host "Waiting for API gateway (http://127.0.0.1:5000)..."
$deadline = (Get-Date).AddMinutes(3)
while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -Uri "http://127.0.0.1:5000/health" -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) { break }
    } catch {}
    Start-Sleep -Seconds 3
}

Write-Host "Stack endpoints:"
Write-Host "  API gateway : http://127.0.0.1:5000"
Write-Host "  Identity    : http://127.0.0.1:5001"
Write-Host "  Admin (docker): http://127.0.0.1:8083"
Write-Host ""
Write-Host "Optional local dev servers (instead of docker admin/mobile):"
Write-Host "  cd admin-app; npx ng serve --port 8083 --host 127.0.0.1"
Write-Host "  cd mobile-app; npm run start"
Write-Host ""
Write-Host "Security E2E:"
Write-Host "  npm run test:e2e:security"
