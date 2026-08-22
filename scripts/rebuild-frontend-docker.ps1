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

Write-Host "Building shared frontend packages..."
Push-Location (Join-Path $PSScriptRoot "..")
try {
    npm run build:shared
    npm run build:mobile-foundation
} finally {
    Pop-Location
}

Write-Host "Rebuilding frontend Docker images affected by foundation i18n changes..."
$env:COMPOSE_PARALLEL_LIMIT = "1"
$services = @("frontend", "admin-app", "dashboard-app")
foreach ($service in $services) {
    Write-Host "Building $service..."
    & $docker compose -f $ComposeFile --env-file $EnvFile build $service
    if ($LASTEXITCODE -ne 0) {
        throw "Docker build failed for $service. Restart Docker Desktop, then rerun this script."
    }
}
& $docker compose -f $ComposeFile --env-file $EnvFile up -d --force-recreate @services

Write-Host "Frontend endpoints:"
Write-Host "  Clinical app : http://127.0.0.1:8081"
Write-Host "  Admin app    : http://127.0.0.1:8083"
Write-Host "  Dashboard    : http://127.0.0.1:8082"
