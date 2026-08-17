[CmdletBinding()]
param(
    [string]$ObservabilityManifest = 'k8s/observability/k3s-observability.yaml',
    [string]$AlertmanagerManifest = 'k8s/observability/production-alertmanager-config.yaml',
    [string]$SyntheticMonitorManifest = 'k8s/jobs/synthetic-monitor-cronjob.yaml',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [ValidateSet('pass','fail')][string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}

if (-not (Test-Path -LiteralPath $ObservabilityManifest -PathType Leaf)) {
    Add-Check 'manifest' 'fail' "Missing observability manifest: $ObservabilityManifest"
} else {
    $text = Get-Content -LiteralPath $ObservabilityManifest -Raw
    $requiredAlerts = @(
        'HisHopeDeploymentUnavailable',
        'HisHopeContainerCrashLoop',
        'HisHopeMigrationJobFailed',
        'HisHopeAdmissionRejections',
        'HisHopeVaultOrRedisDependencyDown',
        'HisHopeErrorBudgetBurnCritical',
        'HisHopeJaegerOomKilled'
    )
    $missing = @($requiredAlerts | Where-Object { $text -notmatch "alert:\s*$([regex]::Escape($_))\b" })
    if ($missing.Count -gt 0) { Add-Check 'required-alerts' 'fail' "Missing alerts: $($missing -join ', ')" }
    else { Add-Check 'required-alerts' 'pass' "$($requiredAlerts.Count) release/dependency/SLO alerts declared." }

    if ($text -match 'gatekeeper_validation_request_count\{admission_status="deny"\}') { Add-Check 'admission-metric' 'pass' 'Gatekeeper deny metric is used for admission rejection alerts.' }
    else { Add-Check 'admission-metric' 'fail' 'Admission rejection alert does not use gatekeeper_validation_request_count/admission_status=deny.' }

    if ($text -match 'namespace=~"his-hope\(-dev\|-staging\)\?"') { Add-Check 'environment-scope' 'pass' 'Workload alerts cover dev, staging and production namespaces.' }
    else { Add-Check 'environment-scope' 'fail' 'Workload alerts do not cover all His.Hope environments.' }

    if ($text -match '(?im)^\s*runbook_url:\s*["'']?https://') { Add-Check 'runbook-links' 'pass' 'Release-health alerts carry runbook links.' }
    else { Add-Check 'runbook-links' 'fail' 'No HTTPS runbook link found for observability alerts.' }

    $images = @([regex]::Matches($text, '(?m)^\s*image:\s*(?<image>[^\s]+)') | ForEach-Object { $_.Groups['image'].Value })
    $unpinned = @($images | Where-Object { $_ -notmatch '@sha256:[0-9a-f]{64}$' })
    if ($unpinned.Count -eq 0) { Add-Check 'image-digests' 'pass' "$($images.Count) observability images are digest-pinned." }
    else { Add-Check 'image-digests' 'fail' "Unpinned observability images: $($unpinned -join ', ')" }
}

if (-not (Test-Path -LiteralPath $AlertmanagerManifest -PathType Leaf)) {
    Add-Check 'alertmanager' 'fail' "Missing Alertmanager manifest: $AlertmanagerManifest"
} else {
    $alertmanager = Get-Content -LiteralPath $AlertmanagerManifest -Raw
    if ($alertmanager -match 'critical-email-discord' -and $alertmanager -match 'send_resolved:\s*true') { Add-Check 'alert-routing' 'pass' 'Critical receiver and resolved notifications are configured.' }
    else { Add-Check 'alert-routing' 'fail' 'Critical receiver or resolved notification is missing.' }
    if ($alertmanager -match '(?i)(smtp_auth_password|DISCORD_WEBHOOK_URL):\s*["'']?\$\{') { Add-Check 'alertmanager-secret-contract' 'pass' 'Alertmanager credentials are placeholders for runtime injection.' }
    else { Add-Check 'alertmanager-secret-contract' 'fail' 'Alertmanager credential is not expressed as a runtime placeholder.' }
}

if (-not (Test-Path -LiteralPath $SyntheticMonitorManifest -PathType Leaf)) {
    Add-Check 'synthetic-monitor' 'fail' "Missing synthetic monitor manifest: $SyntheticMonitorManifest"
} else {
    $synthetic = Get-Content -LiteralPath $SyntheticMonitorManifest -Raw
    if ($synthetic -match '(?im)exit\s+0\s*$') {
        Add-Check 'synthetic-monitor' 'fail' 'Synthetic monitor suppresses failed journeys with exit 0.'
    } elseif ($synthetic -notmatch 'exit\s+"?\$\{EXIT_CODE\}"?') {
        Add-Check 'synthetic-monitor' 'fail' 'Synthetic monitor does not propagate the Playwright exit code.'
    } elseif ($synthetic -notmatch 'secretKeyRef:\s*\r?\n\s+name:\s*synthetic-monitor-credentials' -or
              $synthetic -notmatch 'key:\s*username' -or $synthetic -notmatch 'key:\s*password') {
        Add-Check 'synthetic-monitor' 'fail' 'Synthetic monitor credentials are not sourced from the required secret keys.'
    } elseif (-not (Test-Path -LiteralPath 'k8s/monitoring/synthetic-monitor-secrets.yaml' -PathType Leaf) -or
              (Get-Content -LiteralPath 'k8s/monitoring/synthetic-monitor-secrets.yaml' -Raw) -notmatch 'secret/data/his-hope/observability/synthetic-monitor') {
        Add-Check 'synthetic-monitor' 'fail' 'Synthetic monitor credentials do not have a declared Vault source path.'
    } elseif ($synthetic -match '"(?:@playwright/test|playwright)"\s*:\s*"\^') {
        Add-Check 'synthetic-monitor' 'fail' 'Synthetic monitor dependency versions must be exact, not caret ranges.'
    } elseif (@([regex]::Matches($synthetic, '@sha256:[0-9a-f]{64}')).Count -lt 2) {
        Add-Check 'synthetic-monitor' 'fail' 'Synthetic monitor images are not digest-pinned.'
    } elseif ($synthetic -notmatch 'unauthenticated protected API request' -or
              $synthetic -notmatch '\[401,\s*403\]') {
        Add-Check 'synthetic-monitor' 'fail' 'Synthetic monitor does not exercise the unauthenticated authorization-negative path.'
    } else {
        Add-Check 'synthetic-monitor' 'pass' 'Synthetic login/search/logout journey and unauthenticated authorization-negative path propagate failures, use required secret keys and digest-pinned images.'
    }
}

$releaseMetadataPath = 'k8s/overlays/prod/release-metadata.yaml'
if (-not (Test-Path -LiteralPath $releaseMetadataPath -PathType Leaf)) {
    Add-Check 'release-metadata' 'fail' 'Production release metadata ConfigMap is missing.'
} else {
    $releaseMetadata = Get-Content -LiteralPath $releaseMetadataPath -Raw
    if ($releaseMetadata -match 'HIS_HOPE_RELEASE_SHA:\s*[0-9a-f]{40}\b' -and
        $releaseMetadata -match 'HIS_HOPE_RELEASE_DIGEST:\s*sha256:[0-9a-f]{64}\b') {
        Add-Check 'release-metadata' 'pass' 'Production release SHA and digest are available for telemetry resource attributes.'
    } else {
        Add-Check 'release-metadata' 'fail' 'Production release metadata must contain a commit SHA and aggregate image digest.'
    }
}

$monitoringFiles = @(Get-ChildItem -LiteralPath 'k8s/monitoring' -File -Include '*.yaml','*.yml' -ErrorAction SilentlyContinue)
$monitoringText = ($monitoringFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
if ($monitoringText -match 'CHANGE_ME|xxxxx') {
    Add-Check 'monitoring-secret-values' 'fail' 'Monitoring manifests contain a placeholder secret value.'
} else {
    Add-Check 'monitoring-secret-values' 'pass' 'Monitoring manifests contain no literal placeholder credentials.'
}
$monitoringImages = @([regex]::Matches($monitoringText, '(?m)^\s*image:\s*(?<image>[^\s]+)') | ForEach-Object { $_.Groups['image'].Value })
$monitoringUnpinned = @($monitoringImages | Where-Object { $_ -notmatch '@sha256:[0-9a-f]{64}$' })
if ($monitoringUnpinned.Count -eq 0) { Add-Check 'monitoring-image-digests' 'pass' "$($monitoringImages.Count) monitoring images are digest-pinned." }
else { Add-Check 'monitoring-image-digests' 'fail' "Unpinned monitoring images: $($monitoringUnpinned -join ', ')" }

$jaegerPaths = @('k8s/monitoring/jaeger.yaml', 'k8s/observability/k3s-observability.yaml')
$missingJaeger = @($jaegerPaths | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
if ($missingJaeger.Count -gt 0) {
    Add-Check 'jaeger-memory-budget' 'fail' "Jaeger manifest(s) missing: $($missingJaeger -join ', ')"
} else {
    $jaegerFailures = [System.Collections.Generic.List[string]]::new()
    foreach ($path in $jaegerPaths) {
        $jaegerText = Get-Content -LiteralPath $path -Raw
        if ($path -eq 'k8s/monitoring/jaeger.yaml') {
            $matchesBudget = $jaegerText -match '(?s)name:\s*jaeger.*?requests:\s*\r?\n\s+cpu:\s*200m\s*\r?\n\s+memory:\s*1Gi.*?limits:\s*\r?\n\s+cpu:\s*"?1"?\s*\r?\n\s+memory:\s*2Gi'
        } else {
            $matchesBudget = $jaegerText -match '(?s)name:\s*jaeger.*?requests:\s*\r?\n\s+cpu:\s*100m\s*\r?\n\s+memory:\s*1Gi.*?limits:\s*\r?\n\s+cpu:\s*1\s*\r?\n\s+memory:\s*2Gi'
        }
        if (-not $matchesBudget) { $jaegerFailures.Add($path) }
    }
    if ($jaegerFailures.Count -eq 0) {
        Add-Check 'jaeger-memory-budget' 'pass' 'Both Jaeger manifests use a 1Gi request and 2Gi limit to avoid the observed OOM restart loop.'
    } else {
        Add-Check 'jaeger-memory-budget' 'fail' "Jaeger resource budget is below 1Gi/2Gi in: $($jaegerFailures -join ', ')"
    }
}

$doraCollectorPath = 'scripts/collect-dora-metrics.py'
$doraWorkflowPath = '.github/workflows/dora-metrics.yml'
$doraDashboardPath = 'k8s/monitoring/dora-metrics-dashboard.yaml'
if ((Test-Path -LiteralPath $doraCollectorPath -PathType Leaf) -and
    (Test-Path -LiteralPath $doraWorkflowPath -PathType Leaf) -and
    (Test-Path -LiteralPath $doraDashboardPath -PathType Leaf)) {
    $doraText = (Get-Content -LiteralPath $doraCollectorPath -Raw) + (Get-Content -LiteralPath $doraWorkflowPath -Raw)
    $doraDashboard = Get-Content -LiteralPath $doraDashboardPath -Raw
    $doraMetrics = @('pipeline_deployment_frequency_per_day','pipeline_lead_time_seconds','pipeline_change_failure_rate_ratio','pipeline_mttr_seconds')
    $missingDora = @($doraMetrics | Where-Object { $doraText -notmatch [regex]::Escape($_) -or $doraDashboard -notmatch [regex]::Escape($_) })
    if ($missingDora.Count -eq 0 -and $doraText -match 'GITHUB_TOKEN' -and $doraText -match 'artifacts/dora') {
        Add-Check 'dora-metrics' 'pass' 'DORA producer, auditable artifacts and Grafana queries cover frequency, lead time, change failure rate and MTTR.'
    } else {
        Add-Check 'dora-metrics' 'fail' "DORA contract is incomplete; missing $($missingDora -join ', ')."
    }
} else {
    Add-Check 'dora-metrics' 'fail' 'DORA collector, workflow or dashboard is missing.'
}

$alertE2eWorkflowPath = '.github/workflows/alertmanager-e2e.yml'
$alertE2eScriptPath = 'scripts/test-alertmanager-notification.ps1'
if ((Test-Path -LiteralPath $alertE2eWorkflowPath -PathType Leaf) -and (Test-Path -LiteralPath $alertE2eScriptPath -PathType Leaf)) {
    $alertE2e = (Get-Content -LiteralPath $alertE2eWorkflowPath -Raw) + (Get-Content -LiteralPath $alertE2eScriptPath -Raw)
    if ($alertE2e -match 'ALERTMANAGER_E2E_RECEIVER_URL' -and $alertE2e -match 'notification-delivery' -and $alertE2e -match 'inputs.run_test') {
        Add-Check 'alertmanager-e2e' 'pass' 'Protected Alertmanager E2E workflow can correlate a synthetic alert with a dedicated receiver; execution remains explicit.'
    } else {
        Add-Check 'alertmanager-e2e' 'fail' 'Alertmanager E2E workflow lacks a protected receiver correlation contract.'
    }
} else {
    Add-Check 'alertmanager-e2e' 'fail' 'Alertmanager E2E workflow or test script is missing.'
}

$exceptionsPath = 'k8s/observability/security-exceptions.yaml'
if ((Test-Path -LiteralPath $exceptionsPath -PathType Leaf) -and
    (Get-Content -LiteralPath $exceptionsPath -Raw) -match 'workload:\s*promtail' -and
    (Get-Content -LiteralPath $exceptionsPath -Raw) -match 'owner:\s*platform-observability' -and
    (Get-Content -LiteralPath $exceptionsPath -Raw) -notmatch 'privileged:\s*true') {
    Add-Check 'restricted-exception-inventory' 'pass' 'Promtail host-log exception is explicit, owned and does not enable privileged mode.'
} else {
    Add-Check 'restricted-exception-inventory' 'fail' 'Promtail exception inventory is missing owner/reason/expiry or enables privileged mode.'
}

$failed = @($checks | Where-Object status -eq 'fail')
$status = if ($failed.Count -gt 0) { 'fail' } else { 'pass' }
$result = [pscustomobject]@{ status = $status; checks = @($checks); generatedAtUtc = [DateTime]::UtcNow.ToString('o') }
$json = $result | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $dir = Split-Path -Parent $OutputPath
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -eq 'fail') { exit 80 }
exit 0
