[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ComposeFile = 'docker/docker-compose.yml',
    [string]$Output = 'artifacts/runtime/compose-dependency-failover.json',
    [int]$TimeoutSeconds = 180,
    [string[]]$ProbeUrls = @(
        'http://localhost:5000/health',
        'http://localhost:5050/health',
        'http://localhost:5015/health',
        'http://localhost:5016/health'
    )
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$services = @('redis', 'rabbitmq', 'postgres')
$serviceSloSeconds = @{ redis = 30; rabbitmq = 60; postgres = 120 }
$composeArgs = @('-f', $ComposeFile)
$results = [System.Collections.Generic.List[object]]::new()

function Get-Health([string]$service) {
    $container = (docker compose @composeArgs ps -q $service).Trim()
    if ([string]::IsNullOrWhiteSpace($container)) { throw "Compose service '$service' is not running." }
    $health = (docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $container).Trim()
    [pscustomobject]@{ Container = $container; Health = $health }
}

function Test-Probes {
    $failed = 0
    foreach ($url in $ProbeUrls) {
        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) { $failed++ }
        } catch { $failed++ }
    }
    return $failed
}

foreach ($service in $services) {
    $before = Get-Health $service
    if ($before.Health -ne 'healthy') { throw "$service is not healthy before drill: $($before.Health)" }
    $started = [DateTimeOffset]::UtcNow
    $sloSeconds = [double]$serviceSloSeconds[$service]
    $probeFailures = 0
    $lastProbeFailures = 1
    $probeRecoveredAt = $null
    if ($PSCmdlet.ShouldProcess($service, 'restart dependency for failover drill')) {
        docker compose @composeArgs restart $service | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Failed to restart $service." }
    }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds([math]::Min($TimeoutSeconds, $sloSeconds))
    do {
        Start-Sleep -Seconds 2
        $current = Get-Health $service
        $lastProbeFailures = Test-Probes
        $probeFailures += $lastProbeFailures
        if ($null -eq $probeRecoveredAt -and $current.Health -eq 'healthy' -and $lastProbeFailures -eq 0) {
            $probeRecoveredAt = [DateTimeOffset]::UtcNow
        }
    } while (($current.Health -ne 'healthy' -or $lastProbeFailures -gt 0) -and [DateTimeOffset]::UtcNow -lt $deadline)
    $recovered = $current.Health -eq 'healthy'
    $results.Add([pscustomobject]@{
        service = $service
        container = $before.Container
        before = $before.Health
        after = $current.Health
        recovered = $recovered
        recoverySeconds = [math]::Round(([DateTimeOffset]::UtcNow - $started).TotalSeconds, 2)
        probeFailures = $probeFailures
        probeRecoverySeconds = if ($null -eq $probeRecoveredAt) { $null } else { [math]::Round(($probeRecoveredAt - $started).TotalSeconds, 2) }
        sloSeconds = $sloSeconds
        sloPassed = $recovered -and $null -ne $probeRecoveredAt -and ([DateTimeOffset]::UtcNow - $started).TotalSeconds -le $sloSeconds
        probeUrls = $ProbeUrls
        measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    })
    if (-not $recovered) { throw "$service did not recover within SLO $sloSeconds seconds." }
}

$outputPath = Join-Path (Get-Location) $Output
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $outputPath -Encoding UTF8
Write-Host "Compose dependency failover drill passed: $outputPath"
