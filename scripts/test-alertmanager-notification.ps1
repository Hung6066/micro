[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AlertmanagerUrl,
    [Parameter(Mandatory = $true)][string]$TestReceiverUrl,
    [string]$BearerToken = $env:ALERTMANAGER_BEARER_TOKEN,
    [switch]$RunTest,
    [switch]$AllowProduction,
    [int]$TimeoutSec = 120,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Evidence([string]$Status, [object[]]$Checks, [string]$Detail) {
    $result = [pscustomobject]@{
        status = $Status
        detail = $Detail
        checks = @($Checks)
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    }
    $json = $result | ConvertTo-Json -Depth 8
    if ($OutputPath) {
        $parent = Split-Path -Parent $OutputPath
        if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
        [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
    }
    Write-Output $json
    return $result
}

if (-not $RunTest) {
    $null = Write-Evidence 'skipped' @([pscustomobject]@{ name = 'notification-e2e'; status = 'skipped'; detail = 'RunTest was not requested; no alert was sent.' }) 'Dry-run: notification test was not executed.'
    exit 0
}
if (-not $AllowProduction) { throw 'RunTest requires -AllowProduction because it sends a synthetic alert to the configured production receiver.' }
if ($TimeoutSec -lt 10 -or $TimeoutSec -gt 600) { throw 'TimeoutSec must be between 10 and 600 seconds.' }

$id = 'his-hope-alertmanager-e2e-' + [guid]::NewGuid().ToString('N')
$headers = @{ Accept = 'application/json'; 'Content-Type' = 'application/json' }
if ($BearerToken) { $headers.Authorization = "Bearer $BearerToken" }
$base = $AlertmanagerUrl.TrimEnd('/')
$receiver = $TestReceiverUrl.TrimEnd('/')
$now = [DateTime]::UtcNow
$alert = @(
    [ordered]@{
        labels = [ordered]@{ alertname = 'HisHopeAlertmanagerE2E'; severity = 'critical'; service = 'platform'; e2e_id = $id }
        annotations = [ordered]@{ summary = 'His.Hope Alertmanager notification E2E probe'; description = "Synthetic delivery probe $id"; runbook_url = 'https://docs.his-hope.local/runbooks/alertmanager-e2e' }
        startsAt = $now.ToString('o')
        endsAt = $now.AddMinutes(2).ToString('o')
        generatorURL = 'https://github.com/his-hope/observability/e2e'
    }
)

$checks = [System.Collections.Generic.List[object]]::new()
try {
    $body = $alert | ConvertTo-Json -Depth 8 -AsArray
    $response = Invoke-RestMethod -Method Post -Uri "$base/api/v2/alerts" -Headers $headers -Body $body -TimeoutSec 20
    $checks.Add([pscustomobject]@{ name = 'alertmanager-accepted'; status = 'pass'; detail = 'Alertmanager accepted the synthetic alert.' })

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
    $delivered = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $probe = Invoke-RestMethod -Method Get -Uri "$receiver?e2e_id=$([Uri]::EscapeDataString($id))" -Headers @{ Accept = 'application/json' } -TimeoutSec 10
            if ($probe.received -eq $true -and $probe.e2e_id -eq $id -and $probe.status -in @('firing', 'resolved')) { $delivered = $true; break }
        } catch { }
        Start-Sleep -Seconds 5
    }
    if ($delivered) {
        $checks.Add([pscustomobject]@{ name = 'notification-delivery'; status = 'pass'; detail = 'Dedicated test receiver observed the alert with the expected correlation id.' })
        $resolved = $alert[0].Clone()
        $resolved.endsAt = [DateTime]::UtcNow.ToString('o')
        Invoke-RestMethod -Method Post -Uri "$base/api/v2/alerts" -Headers $headers -Body (@($resolved) | ConvertTo-Json -Depth 8 -AsArray) -TimeoutSec 20 | Out-Null
        $resolvedDeadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(30, [int]($TimeoutSec / 2)))
        $resolvedDelivered = $false
        while ([DateTime]::UtcNow -lt $resolvedDeadline) {
            try {
                $resolvedProbe = Invoke-RestMethod -Method Get -Uri "$receiver?e2e_id=$([Uri]::EscapeDataString($id))" -Headers @{ Accept = 'application/json' } -TimeoutSec 10
                if ($resolvedProbe.received -eq $true -and $resolvedProbe.e2e_id -eq $id -and $resolvedProbe.status -eq 'resolved') { $resolvedDelivered = $true; break }
            } catch { }
            Start-Sleep -Seconds 5
        }
        if ($resolvedDelivered) {
            $checks.Add([pscustomobject]@{ name = 'resolved-notification-delivery'; status = 'pass'; detail = 'Dedicated test receiver observed the resolved notification for the same correlation id.' })
        } else {
            $checks.Add([pscustomobject]@{ name = 'resolved-notification-delivery'; status = 'fail'; detail = "Test receiver did not observe the resolved notification for $id." })
        }
    } else {
        $checks.Add([pscustomobject]@{ name = 'notification-delivery'; status = 'fail'; detail = "Test receiver did not observe $id within $TimeoutSec seconds." })
    }
} catch {
    $checks.Add([pscustomobject]@{ name = 'alertmanager-accepted'; status = 'fail'; detail = $_.Exception.Message })
}

$status = if (@($checks | Where-Object status -eq 'fail').Count -gt 0) { 'fail' } else { 'pass' }
$null = Write-Evidence $status $checks "Alertmanager acceptance and receiver delivery were tested for correlation id $id."
if ($status -eq 'fail') { exit 80 }
exit 0
