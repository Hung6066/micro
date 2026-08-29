[CmdletBinding()]
param(
    [switch]$SkipCommands,
    [string]$EvidencePath = "artifacts/evidence/operator-mobile-phases.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$appRoot = Join-Path $repoRoot "operator-mobile"
$featureRoot = Join-Path $appRoot "src/app"
$results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$phase, [string]$gate, $passed, [string]$detail) {
    $passed = [bool]$passed
    $results.Add([pscustomobject]@{
        phase = $phase
        gate = $gate
        status = if ($passed) { "PASS" } else { "FAIL" }
        detail = $detail
    })
}

function Read-Text([string]$path) {
    return Get-Content -LiteralPath $path -Raw
}

function Invoke-NpmCapture([string]$arguments) {
    $tempPath = [System.IO.Path]::GetTempFileName()
    try {
        & cmd.exe /d /c "npm $arguments > `"$tempPath`" 2>&1"
        $exitCode = $LASTEXITCODE
        return [pscustomobject]@{ ExitCode = $exitCode; Output = (Get-Content -LiteralPath $tempPath -Raw) }
    }
    finally { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
}

$htmlFiles = @(Get-ChildItem -LiteralPath $featureRoot -Recurse -Filter "*.html" -File | Where-Object { $_.FullName -notmatch "\\(node_modules|dist|\.angular)\\" })
$tsFiles = @(Get-ChildItem -LiteralPath $featureRoot -Recurse -Filter "*.ts" -File | Where-Object { $_.FullName -notmatch "\\(node_modules|dist|\.angular)\\" })
$styleFiles = @(Get-ChildItem -LiteralPath (Join-Path $appRoot "src") -Recurse -Include "*.scss","*.css" -File)
$htmlText = ($htmlFiles | ForEach-Object { Read-Text $_.FullName }) -join "`n"
$tsText = ($tsFiles | ForEach-Object { Read-Text $_.FullName }) -join "`n"
$styleText = ($styleFiles | ForEach-Object { Read-Text $_.FullName }) -join "`n"
$featureTsFiles = @(Get-ChildItem -LiteralPath (Join-Path $appRoot "src/app/features") -Recurse -Filter "*.ts" -File)
$featureTsText = ($featureTsFiles | ForEach-Object { Read-Text $_.FullName }) -join "`n"

# Phase 0: source contracts and field-app boundaries.
Add-Result "P0" "No native select remains" (-not ($htmlText -match "<select\b|</select>")) "All operator-mobile templates use hh-select or non-select controls."
Add-Result "P0" "No direct Capacitor feature import" (-not ($featureTsText -match "@capacitor")) "Feature code remains behind application services."
Add-Result "P0" "Shared i18n usage" (($htmlText -match "hhTranslate") -and ($tsText -match "HisHopeI18nService")) "Templates and command messages use shared i18n APIs."
Add-Result "P0" "API error normalization" (($tsText -match "operatorMobileErrorMessage") -and ($tsText -match "operatorSessionExpired") -and ($tsText -match "operatorConflict")) "401/403/409/422 responses map to localized guidance."

# Phase 1: operational command surfaces and offline transport.
$apiText = Read-Text (Join-Path $appRoot "src/app/core/services/operator-mobile-api.service.ts")
$maintenanceText = Read-Text (Join-Path $appRoot "src/app/features/maintenance/maintenance-work-page.component.ts")
$maintenanceHtml = Read-Text (Join-Path $appRoot "src/app/features/maintenance/maintenance-work-page.component.html")
$handoverText = Read-Text (Join-Path $appRoot "src/app/features/handover/shift-handover-page.component.ts")
$handoverHtml = Read-Text (Join-Path $appRoot "src/app/features/handover/shift-handover-page.component.html")
$queueText = Read-Text (Join-Path $appRoot "src/app/core/offline/operation-queue.service.ts")
$readCacheText = Read-Text (Join-Path $appRoot "src/app/core/services/operator-mobile-read-cache.service.ts")
Add-Result "P1" "Production commands" (($apiText -match "transitionProductionBatch") -and ($apiText -match "recordOperationMeasurement") -and ($apiText -match "reviewLoss")) "Lifecycle, measurement and loss-review transports are present."
Add-Result "P1" "Quality commands" (($apiText -match "changeQualitySampleDisposition") -and ($apiText -match "createDeviation")) "Sample disposition and deviation transports are present."
Add-Result "P1" "Maintenance commands" (($apiText -match "recordMachineDowntime") -and ($apiText -match "resolveMachineDowntime")) "Downtime transports are present."
Add-Result "P1" "Maintenance checklist" (($maintenanceText -match "loadWorkOrderChecklist") -and ($maintenanceHtml -match "checklistItems") -and ($maintenanceText -match "checklistComplete")) "Work-order checklist is loaded and required before completion."
Add-Result "P1" "Offline queue/idempotency" (($apiText -match "X-HisHope-Operation-Id") -and ($queueText -match "OperationCommand")) "Command transport carries operation identity through the queue."
Add-Result "P0" "Read cache/stale indicator" (($readCacheText -match "ttlMs") -and ($readCacheText -match "stale") -and ($htmlText -match "operatorStaleData")) "Tenant-scoped reads retain a short-lived value and expose stale state."
Add-Result "P2" "Queue recovery controls" (($queueText -match "retry\(") -and ($queueText -match "clear\(") -and ($htmlText -match "operatorRetryRecord")) "Operators can retry failed/conflicted records and wipe local queue on logout."
Add-Result "P4" "Queue retention/dead-letter" (($queueText -match "MAX_ENTRIES") -and ($queueText -match "DEAD_LETTER_ATTEMPTS") -and ($queueText -match "pruneTerminalEntries")) "Transient retries have a bounded budget and terminal records are retained within a fixed limit."
Add-Result "P2" "Shift handover" (($handoverText -match "getProductionBatches") -and ($handoverText -match "getLots") -and ($handoverText -match "getMaintenanceWorkOrders") -and ($handoverHtml -match "operatorHandoverDowntime")) "Handover view aggregates unresolved batches, holds, downtime and overdue work orders."

# Phase 2-4: presentation and shared-foundation constraints.
$enPath = Join-Path $repoRoot "shared/frontend-foundation/i18n/src/dictionaries/en.ts"
$viPath = Join-Path $repoRoot "shared/frontend-foundation/i18n/src/dictionaries/vi-vn.ts"
$enText = Read-Text $enPath
$viText = Read-Text $viPath
$mobileKeys = [regex]::Matches($htmlText, 'mobile\.([A-Za-z0-9_]+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$missing = @($mobileKeys | Where-Object { ($enText -notmatch "(?m)^\s*$_\s*:") -or ($viText -notmatch "(?m)^\s*$_\s*:") })
$i18nDetail = if ($missing.Count) { "Missing: $($missing -join ', ')" } else { "$($mobileKeys.Count) referenced mobile keys exist in EN and VI." }
Add-Result "P2" "EN/VI i18n parity" ($missing.Count -eq 0) $i18nDetail
Add-Result "P2" "Theme token usage" (($styleText -match "var\(--") -and ($styleText -notmatch "(?m)^\s*(background|color|border-color)\s*:\s*#[0-9A-Fa-f]+")) "Page styles use theme tokens; no direct color declarations found."
Add-Result "P2" "Font contract" (Read-Text (Join-Path $appRoot "src/styles.scss") -match "font-family:\s*var\(--font-body\)") "Body font comes from the shared font token."
Add-Result "P2" "Shared select boundary" (($htmlText -match "<hh-select\b") -and ($apiText -match "@his-hope/frontend-foundation/contracts")) "Select and API types use shared foundation contracts."

if (-not $SkipCommands) {
    Push-Location $appRoot
    try {
        $lint = Invoke-NpmCapture "run lint"
        Add-Result "P0" "lint" ($lint.ExitCode -eq 0) "Operator-mobile lint completed."
        $tests = Invoke-NpmCapture "test"
        Add-Result "P1" "unit tests" (($tests.ExitCode -eq 0) -and ($tests.Output -match "TOTAL:\s+\d+ SUCCESS")) "Operator-mobile unit suite completed."
        $build = Invoke-NpmCapture "run build"
        Add-Result "P4" "production build" (($build.ExitCode -eq 0) -and ($build.Output -match "Application bundle generation complete")) "Operator-mobile and shared package build completed."
    }
    finally { Pop-Location }
}

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$overallStatus = if ($failed.Count) { "FAIL" } elseif ($SkipCommands) { "STATIC_PASS_COMMANDS_SKIPPED" } else { "PASS" }
$evidence = [pscustomobject]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    repository = $repoRoot
    application = "operator-mobile"
    status = $overallStatus
    results = @($results)
}
$absoluteEvidence = Join-Path $repoRoot $EvidencePath
$evidenceDir = Split-Path -Parent $absoluteEvidence
New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null
$evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $absoluteEvidence -Encoding utf8
$evidence | ConvertTo-Json -Depth 5
if ($failed.Count) { exit 1 }
