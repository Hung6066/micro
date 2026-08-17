[CmdletBinding()]
param(
    [string]$ComposeFile = (Join-Path $PSScriptRoot '..\..\docker\docker-compose.yml'),
    [string]$Environment = 'development',
    [string]$ProjectName = 'his-hope-smoke',
    [switch]$KeepRunning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$renderScript = Join-Path $repoRoot 'docker\config\compose.runtime.env.ps1'
$renderedEnvironment = Join-Path ([IO.Path]::GetTempPath()) "his-hope-compose-$([guid]::NewGuid().ToString('N')).env"
$smokeComposeFile = Join-Path ([IO.Path]::GetTempPath()) "his-hope-compose-smoke-$([guid]::NewGuid().ToString('N')).yml"
@'
services:
  jaeger:
    image: jaegertracing/all-in-one:1.55
    ports: ["16686:16686"]
    environment:
      COLLECTOR_OTLP_ENABLED: "true"
  prometheus:
    image: prom/prometheus:v2.51.0
    ports: ["9090:9090"]
  loki:
    image: grafana/loki:3.1.0
    command: ["-config.file=/etc/loki/local-config.yaml"]
    ports: ["3100:3100"]
  alertmanager:
    image: prom/alertmanager:v0.27.0
    ports: ["9093:9093"]
'@ | Set-Content -LiteralPath $smokeComposeFile -Encoding utf8

function Invoke-Compose {
    param([string[]]$Arguments)
    & docker compose --project-name $ProjectName --env-file $renderedEnvironment --file $smokeComposeFile @Arguments
    if ($LASTEXITCODE -ne 0) { throw "docker compose failed: $($Arguments -join ' ')" }
}

function Wait-ForHttp {
    param([string]$Name, [string]$Url)
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                Write-Output "PASS compose:$Name status=$($response.StatusCode)"
                return
            }
        } catch { }
        Start-Sleep -Seconds 2
    }
    throw "$Name did not become ready at $Url."
}

try {
    & pwsh -NoProfile -File $renderScript -Environment $Environment -OutputFile $renderedEnvironment | Write-Output
    if ($LASTEXITCODE -ne 0) { throw 'Compose runtime environment rendering failed.' }

    Invoke-Compose @('config', '--quiet')
    Invoke-Compose @('up', '-d', 'jaeger', 'prometheus', 'loki', 'alertmanager')

    $checks = @(
        @{ Name = 'prometheus'; Url = 'http://localhost:9090/-/ready' },
        @{ Name = 'loki'; Url = 'http://localhost:3100/ready' },
        @{ Name = 'jaeger'; Url = 'http://localhost:16686/api/services' },
        @{ Name = 'alertmanager'; Url = 'http://localhost:9093/-/ready' }
    )

    foreach ($check in $checks) {
        Wait-ForHttp -Name $check.Name -Url $check.Url
    }

    Write-Output "PASS compose:config project=$ProjectName"
}
finally {
    if (-not $KeepRunning) {
        try { Invoke-Compose @('down', '--remove-orphans') } catch { Write-Warning $_ }
    }
    Remove-Item -LiteralPath $renderedEnvironment -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $smokeComposeFile -Force -ErrorAction SilentlyContinue
}
