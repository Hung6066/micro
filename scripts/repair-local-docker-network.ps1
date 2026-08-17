[CmdletBinding()]
param(
    [int]$Port = 8083,
    [int]$TimeoutSeconds = 90,
    [switch]$StartCompose
)

$ErrorActionPreference = 'Stop'
$composeFile = Join-Path $PSScriptRoot '..\docker\docker-compose.yml'
$envFile = Join-Path $PSScriptRoot '..\docker\config\compose.runtime.env'

function Test-HttpPort {
    param([int]$TargetPort)
    $status = & curl.exe -sS -o NUL -w '%{http_code}' --connect-timeout 3 --max-time 5 "http://127.0.0.1:$TargetPort/" 2>$null
    if ($LASTEXITCODE -ne 0) { return $false }
    $code = 0
    if (-not [int]::TryParse(($status -join '').Trim(), [ref]$code)) { return $false }
    return $code -ge 200 -and $code -lt 500
}

$initialFailures = 0
1..2 | ForEach-Object {
    if (Test-HttpPort -TargetPort $Port) { $initialFailures = 0 } else { $initialFailures++ }
    if ($_ -lt 2) { Start-Sleep -Seconds 2 }
}
if ($initialFailures -lt 2) {
    Write-Host "Port $Port is reachable; Docker host forwarding is healthy."
    exit 0
}

$dockerDesktop = Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq 'Docker Desktop.exe' -and $_.ExecutablePath -like '*DockerDesktop*' } |
    Select-Object -First 1 -ExpandProperty ExecutablePath

if (-not $dockerDesktop) {
    $dockerDesktop = Get-ChildItem "$env:LOCALAPPDATA\Programs\DockerDesktop\Docker Desktop.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $dockerDesktop) {
    throw 'Docker Desktop executable was not found. Start Docker Desktop manually and rerun this script.'
}

Write-Host "Port $Port is unreachable; restarting Docker Desktop backend..."
try {
    & docker desktop restart | Out-Host
} catch {
    Start-Process -FilePath $dockerDesktop | Out-Null
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
do {
    Start-Sleep -Seconds 2
    if (Test-HttpPort -TargetPort $Port) {
        Write-Host "Port $Port recovered after Docker Desktop restart."
        if ($StartCompose) {
            & docker compose -f $composeFile --env-file $envFile up -d | Out-Host
        }
        exit 0
    }
} while ((Get-Date) -lt $deadline)

throw "Port $Port did not recover within $TimeoutSeconds seconds. Check Docker Desktop and WSL2 networking; container health alone is not sufficient."
