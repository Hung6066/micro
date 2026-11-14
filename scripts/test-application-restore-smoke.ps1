[CmdletBinding()]
param(
    [ValidateSet('staging', 'production')][string]$Environment = 'staging',
    [Parameter(Mandatory)][string]$ContinuityUrl,
    [Parameter(Mandatory)][string]$ApplicationUrl,
    [Parameter(Mandatory)][string]$OidcDiscoveryUrl,
    [Parameter(Mandatory)][string]$AuthenticatedSmokePath,
    [string]$BearerToken = $env:APP_RESTORE_BEARER_TOKEN,
    [Parameter(Mandatory)][string]$Kubeconfig,
    [string]$Namespace = 'his-hope',
    [double]$RpoMinutes = 0,
    [int]$TimeoutSec = 1800,
    [string]$OutputPath = 'artifacts/evidence/application-restore-smoke.json',
    [switch]$Apply,
    [switch]$AllowProduction,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Environment -eq 'production' -and (-not $AllowProduction -or -not $Apply)) {
    throw 'Production application restore is blocked unless both -Apply and -AllowProduction are supplied by the protected workflow.'
}
if ([string]::IsNullOrWhiteSpace($BearerToken)) { throw 'BearerToken is required for the authenticated restore smoke.' }
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }
if ($TimeoutSec -lt 60 -or $TimeoutSec -gt 3600) { throw 'TimeoutSec must be between 60 and 3600 seconds.' }
if ($RpoMinutes -lt 0 -or [double]::IsNaN($RpoMinutes) -or [double]::IsInfinity($RpoMinutes)) { throw 'RpoMinutes must be a finite non-negative number.' }

$output = [IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $output
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
$startedAt = [DateTime]::UtcNow
$status = 'fail'
$verified = $false
$failure = $null
$checks = [System.Collections.Generic.List[object]]::new()

function Write-Evidence {
    $rto = if ($verified) { ([DateTime]::UtcNow - $startedAt).TotalMinutes } else { 0 }
    $doc = [pscustomobject]@{
        status = $status
        executedAtUtc = $startedAt.ToString('o')
        rpoMinutes = $RpoMinutes
        rtoMinutes = [math]::Round($rto, 3)
        restoreVerified = $verified
        target = "$Environment/application/$Namespace"
        recoveryMode = 'database-continuity-restore-job-plus-application-smoke'
        checks = @($checks)
        failure = $failure
    }
    [IO.File]::WriteAllText($output, ($doc | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
}

if ($WhatIf -or -not $Apply) {
    $status = 'skipped'
    $checks.Add([pscustomobject]@{ name = 'restore-smoke'; status = 'skipped'; detail = 'No restore job or application endpoint was called.' })
    Write-Evidence
    Write-Output 'DRILL DRY-RUN: restore job, readiness, OIDC and authorization smoke would run in the selected environment.'
    exit 0
}

$headers = @{ Authorization = "Bearer $BearerToken"; Accept = 'application/json'; 'Content-Type' = 'application/json'; 'X-Correlation-Id' = "restore-smoke-$([guid]::NewGuid().ToString('N'))" }
$baseContinuity = $ContinuityUrl.TrimEnd('/')
$baseApplication = $ApplicationUrl.TrimEnd('/')
$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path

try {
    $restore = Invoke-WebRequest -Method Post -Uri "$baseContinuity/api/v1/admin/database-continuity/restore-drills" -Headers $headers -Body '{}' -TimeoutSec 30
    if ([int]$restore.StatusCode -ne 202) { throw "Restore endpoint returned HTTP $($restore.StatusCode), expected 202." }
    $job = $restore.Content | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$job.jobId)) { throw 'Restore endpoint did not return a jobId.' }
    $checks.Add([pscustomobject]@{ name = 'restore-job-accepted'; status = 'pass'; detail = 'Database continuity restore job was accepted.' })

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
    $completed = $false
    $jobStatus = ''
    while ([DateTime]::UtcNow -lt $deadline) {
        $jobDoc = Invoke-RestMethod -Method Get -Uri "$baseContinuity/api/v1/admin/database-continuity/jobs/$($job.jobId)" -Headers @{ Authorization = "Bearer $BearerToken"; Accept = 'application/json' } -TimeoutSec 30
        $jobStatus = [string]$jobDoc.status
        if ($jobStatus -match '(?i)^completed$|^3$') { $completed = $true; break }
        if ($jobStatus -match '(?i)^failed$|^4$') { throw "Restore job failed (status=$jobStatus, errorCode=$($jobDoc.errorCode))." }
        Start-Sleep -Seconds 10
    }
    if (-not $completed) { throw "Restore job did not complete before timeout (status=$jobStatus)." }
    $checks.Add([pscustomobject]@{ name = 'restore-job-completed'; status = 'pass'; detail = 'Restore job completed successfully.' })

    $health = Invoke-WebRequest -Method Get -Uri "$baseApplication/health" -Headers @{ Accept = 'application/json' } -TimeoutSec 30
    if ([int]$health.StatusCode -ne 200) { throw "Application health returned HTTP $($health.StatusCode)." }
    $checks.Add([pscustomobject]@{ name = 'application-readiness'; status = 'pass'; detail = 'Application health returned HTTP 200 after restore.' })

    $discovery = Invoke-RestMethod -Method Get -Uri $OidcDiscoveryUrl -Headers @{ Accept = 'application/json' } -TimeoutSec 30
    if ([string]::IsNullOrWhiteSpace([string]$discovery.issuer)) { throw 'OIDC discovery response did not contain issuer.' }
    $checks.Add([pscustomobject]@{ name = 'oidc-discovery'; status = 'pass'; detail = 'OIDC discovery returned an issuer after restore.' })

    $unauth = $null
    try { $unauth = Invoke-WebRequest -Method Get -Uri "$baseApplication/api/v1/patients/search?q=&page=1&pageSize=1" -Headers @{ Accept = 'application/json' } -TimeoutSec 30 } catch { $unauth = $_.Exception.Response }
    if ($null -eq $unauth -or [int]$unauth.StatusCode -notin @(401, 403)) { throw 'Unauthenticated protected API did not return 401/403.' }
    $checks.Add([pscustomobject]@{ name = 'authorization-negative'; status = 'pass'; detail = 'Unauthenticated protected API returned 401/403.' })

    $smoke = Invoke-WebRequest -Method Get -Uri "$baseApplication/$($AuthenticatedSmokePath.TrimStart('/'))" -Headers @{ Authorization = "Bearer $BearerToken"; Accept = 'application/json' } -TimeoutSec 60
    if ([int]$smoke.StatusCode -lt 200 -or [int]$smoke.StatusCode -ge 300) { throw "Authenticated smoke endpoint returned HTTP $($smoke.StatusCode)." }
    $checks.Add([pscustomobject]@{ name = 'authenticated-api-smoke'; status = 'pass'; detail = 'Authenticated application API smoke returned 2xx.' })

    $deployments = & kubectl --kubeconfig $env:KUBECONFIG -n $Namespace get deployments -o json --request-timeout=30s 2>$null | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect application deployments after restore.' }
    $notReady = @($deployments.items | Where-Object {
        $availableReplicas = 0
        $desiredReplicas = 0
        if ($null -ne $_.status.availableReplicas) { $availableReplicas = [int]$_.status.availableReplicas }
        if ($null -ne $_.spec.replicas) { $desiredReplicas = [int]$_.spec.replicas }
        $availableReplicas -lt $desiredReplicas
    })
    if ($notReady.Count -gt 0) { throw "Application deployments are not fully available after restore: $($notReady.metadata.name -join ', ')." }
    $checks.Add([pscustomobject]@{ name = 'deployment-readiness'; status = 'pass'; detail = 'All application Deployments report available replicas.' })

    $status = 'pass'
    $verified = $true
    Write-Evidence
    Write-Output 'Application restore smoke PASS: restore job, readiness, OIDC, authorization and authenticated API checks passed.'
}
catch {
    $failure = $_.Exception.Message -replace '(?i)(password|token|secret|sas|private[_-]?key|authorization)[^;\r\n]*', '$1=[redacted]'
    Write-Evidence
    throw
}
