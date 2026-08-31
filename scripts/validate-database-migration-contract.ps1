[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$MigrationDirectory = 'artifacts/database-migrations-current',
    [string]$RenderedProductionManifest = 'artifacts/k8s/prod.yaml',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Get-Sha256([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($Path))) -replace '-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [ValidateSet('pass','fail','blocked')][string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}

$root = (Resolve-Path $RepositoryRoot).Path
$directory = Join-Path $root $MigrationDirectory
$manifestPath = Join-Path $directory 'migration-manifest.json'
$expected = @('identity','appointment','clinical','lab','billing','patient','patient-read','pharmacy')

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    Add-Check 'migration-manifest' 'fail' "Manifest not found: $manifestPath"
} else {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $names = @($manifest.contexts | ForEach-Object name | Sort-Object)
    $missing = @($expected | Where-Object { $names -notcontains $_ })
    $extra = @($names | Where-Object { $expected -notcontains $_ })
    if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
        Add-Check 'migration-context-coverage' 'fail' "Missing=[$($missing -join ',')]; extra=[$($extra -join ',')]"
    } else { Add-Check 'migration-context-coverage' 'pass' "$($expected.Count) DbContext scripts are declared." }

    $hashFailures = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in @($manifest.contexts)) {
        $scriptPath = Join-Path $directory $entry.script
        if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) { $hashFailures.Add("missing:$($entry.script)"); continue }
        $actual = Get-Sha256 $scriptPath
        if ($actual -ne $entry.sha256.ToLowerInvariant()) { $hashFailures.Add("hash:$($entry.script)") }
    }
    if ($hashFailures.Count -gt 0) { Add-Check 'migration-artifact-integrity' 'fail' ($hashFailures -join ', ') }
    else { Add-Check 'migration-artifact-integrity' 'pass' 'All migration scripts match the signed-off manifest hashes.' }

    # A signed hash only proves that the artifact matches its manifest. Also
    # ensure it was generated after the migration source, otherwise a stale
    # assembly/artifact pair can pass checksum validation.
    $sourceRoots = @{
        identity = 'src/Services/IdentityService/IdentityService.Infrastructure/Persistence/Migrations'
        appointment = 'src/Services/AppointmentService/AppointmentService.Infrastructure/Persistence/Migrations'
        clinical = 'src/Services/ClinicalService/ClinicalService.Infrastructure/Persistence/Migrations'
        lab = 'src/Services/LabService/LabService.Infrastructure/Persistence/Migrations'
        billing = 'src/Services/BillingService/BillingService.Infrastructure/Persistence/Migrations'
        patient = 'src/Services/PatientService/PatientService.Infrastructure/Persistence/Migrations'
        'patient-read' = 'src/Services/PatientService/PatientService.Infrastructure/Persistence/Migrations'
        pharmacy = 'src/Services/PharmacyService/PharmacyService.Infrastructure/Persistence/Migrations'
    }
    $staleArtifacts = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in @($manifest.contexts)) {
        if (-not $sourceRoots.ContainsKey([string]$entry.name)) { continue }
        $artifactPath = Join-Path $directory $entry.script
        $sourceDirectory = Join-Path $root $sourceRoots[[string]$entry.name]
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf) -or
            -not (Test-Path -LiteralPath $sourceDirectory -PathType Container)) { continue }
        # EF tooling may rewrite the model snapshot during a design-time build;
        # freshness is about hand-authored migration source, not generated snapshots.
        $latestSource = Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.cs' -File |
            Where-Object { $_.Name -notmatch 'ModelSnapshot\.cs$' } |
            Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        if ($null -ne $latestSource -and (Get-Item -LiteralPath $artifactPath).LastWriteTimeUtc -lt $latestSource.LastWriteTimeUtc) {
            $staleArtifacts.Add([string]$entry.script)
        }
    }
    if ($staleArtifacts.Count -gt 0) { Add-Check 'migration-artifact-freshness' 'fail' "Artifacts older than migration source: $($staleArtifacts -join ', ')" }
    else { Add-Check 'migration-artifact-freshness' 'pass' 'Migration artifacts are newer than their source migrations.' }
}

$sqlFiles = @(Get-ChildItem -LiteralPath $directory -Filter '*-idempotent.sql' -File -ErrorAction SilentlyContinue)
$destructive = [System.Collections.Generic.List[string]]::new()
foreach ($file in $sqlFiles) {
    $matches = Select-String -LiteralPath $file.FullName -Pattern '(?i)\b(drop\s+(table|column|schema)|truncate\s+table|delete\s+from)\b' -AllMatches
    if ($matches) { $destructive.Add($file.Name) }
}
if ($destructive.Count -gt 0) { Add-Check 'destructive-migration-review' 'blocked' "Destructive SQL requires an explicit expand/contract review: $($destructive -join ', ')" }
else { Add-Check 'destructive-migration-review' 'pass' 'No destructive SQL pattern detected in generated scripts.' }

# Every hand-authored EF migration must expose a rollback body. This does not
# execute Down() in production; it prevents an upgrade from being shipped with
# an irreversible schema step that cannot be rehearsed on a restore copy.
$migrationRoots = @(
    'src/Services/IdentityService/IdentityService.Infrastructure/Persistence/Migrations',
    'src/Services/AppointmentService/AppointmentService.Infrastructure/Persistence/Migrations',
    'src/Services/ClinicalService/ClinicalService.Infrastructure/Persistence/Migrations',
    'src/Services/LabService/LabService.Infrastructure/Persistence/Migrations',
    'src/Services/BillingService/BillingService.Infrastructure/Persistence/Migrations',
    'src/Services/PatientService/PatientService.Infrastructure/Persistence/Migrations',
    'src/Services/PharmacyService/PharmacyService.Infrastructure/Persistence/Migrations'
)
$rollbackMissing = [System.Collections.Generic.List[string]]::new()
foreach ($relativeRoot in $migrationRoots) {
    $migrationRoot = Join-Path $root $relativeRoot
    if (-not (Test-Path -LiteralPath $migrationRoot -PathType Container)) { continue }
    foreach ($migrationFile in @(Get-ChildItem -LiteralPath $migrationRoot -Filter '*.cs' -File | Where-Object { $_.Name -notmatch 'Designer|Snapshot' })) {
        if ((Get-Content -LiteralPath $migrationFile.FullName -Raw) -notmatch 'protected override void Down\(') {
            $rollbackMissing.Add($migrationFile.FullName.Substring($root.Length + 1))
        }
    }
}
if ($rollbackMissing.Count -gt 0) {
    Add-Check 'migration-rollback-surface' 'fail' "Migrations without Down(): $($rollbackMissing -join ', ')"
} else {
    Add-Check 'migration-rollback-surface' 'pass' 'All hand-authored EF migrations expose a Down() rollback body.'
}

# Every API that owns an EF context must expose a one-shot migration-only mode
# for the reviewed Kubernetes Job. This keeps DDL out of long-running API
# replicas and prevents any environment from silently creating an unmanaged schema.
$migrationOnlySources = @(
    'src/Services/IdentityService/IdentityService.Api/Program.cs',
    'src/Services/IdentityService/IdentityService.Infrastructure/Persistence/IdentityDbInitializer.cs',
    'src/Services/AppointmentService/AppointmentService.Api/Program.cs',
    'src/Services/ClinicalService/ClinicalService.Api/Program.cs',
    'src/Services/LabService/LabService.Api/Program.cs',
    'src/Services/BillingService/BillingService.Api/Program.cs',
    'src/Services/PatientService/PatientService.Api/Program.cs',
    'src/Services/PharmacyService/PharmacyService.Api/Program.cs',
    'src/Services/CommerceService/CommerceService.Api/Program.cs',
    'src/Services/ContentService/ContentService.Api/Program.cs',
    'src/Services/ManufacturingService/ManufacturingService.Api/Program.cs'
)
$missingMigrationOnly = [System.Collections.Generic.List[string]]::new()
foreach ($relative in $migrationOnlySources) {
    $sourcePath = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        $missingMigrationOnly.Add("missing:$relative")
        continue
    }
    $source = Get-Content -LiteralPath $sourcePath -Raw
    if ($source -notmatch 'Persistence:MigrationOnly' -or $source -notmatch '(?m)\breturn;') {
        $missingMigrationOnly.Add($relative)
    }
}
if ($missingMigrationOnly.Count -gt 0) {
    Add-Check 'migration-only-runner' 'fail' "One-shot migration mode is missing from: $($missingMigrationOnly -join ', ')"
} else {
    Add-Check 'migration-only-runner' 'pass' 'All EF owners expose a migration-only exit path.'
}

$renderPath = Join-Path $root $RenderedProductionManifest
if (-not (Test-Path -LiteralPath $renderPath -PathType Leaf)) {
    Add-Check 'api-migration-isolation' 'fail' "Rendered production manifest not found: $renderPath"
} else {
    $rendered = Get-Content -LiteralPath $renderPath -Raw
    $documents = @($rendered -split '(?m)^---\s*$' | Where-Object { $_ -match '(?m)^kind:\s*\S+' })
    $apiDocuments = @($documents | Where-Object { $_ -match '(?m)^kind:\s*Deployment\s*$' })
    $migrationJobs = @($documents | Where-Object { $_ -match '(?m)^kind:\s*Job\s*$' -and $_ -match 'production-migrate-' })
    $migrationFlags = [regex]::Matches(($apiDocuments -join "`n---`n"), '(?ms)name:\s*Persistence__RunMigrationsOnStartup\s*\r?\n\s*value:\s*["'']?(?<value>true|false)["'']?')
    $enabled = @($migrationFlags | Where-Object { $_.Groups['value'].Value -eq 'true' })
    if ($migrationFlags.Count -lt 7) { Add-Check 'api-migration-isolation' 'fail' "Expected at least 7 production API migration flags, found $($migrationFlags.Count)." }
    elseif ($enabled.Count -gt 0) { Add-Check 'api-migration-isolation' 'fail' "Production API startup migrations are enabled in $($enabled.Count) workload(s)." }
    else { Add-Check 'api-migration-isolation' 'pass' "All $($migrationFlags.Count) production API migration flags are false." }

    $migrationOnlyFlags = [regex]::Matches(($apiDocuments -join "`n---`n"), '(?ms)name:\s*Persistence__MigrationOnly\s*\r?\n\s*value:\s*["'']?(?<value>true|false)["'']?')
    $migrationOnlyEnabled = @($migrationOnlyFlags | Where-Object { $_.Groups['value'].Value -eq 'true' })
    if ($migrationOnlyEnabled.Count -gt 0) {
        Add-Check 'api-migration-only-isolation' 'fail' "Production API migration-only mode is enabled in $($migrationOnlyEnabled.Count) workload(s)."
    } else {
        Add-Check 'api-migration-only-isolation' 'pass' 'No production API Deployment enables migration-only mode.'
    }

    $jobNames = @($migrationJobs | ForEach-Object {
        if ($_ -match '(?m)^\s*name:\s*(?:\S+-)?production-migrate-(?<name>[a-z-]+)\s*$') { $Matches['name'] }
    } | Sort-Object -Unique)
    $expectedJobs = @('appointment','billing','clinical','identity','lab','patient','pharmacy')
    # If a production overlay starts deploying Commerce, Content or
    # Manufacturing, require a matching one-shot migration Job before the
    # deployment can pass the contract gate. These services are not currently
    # part of the reviewed K8s production workload, so no speculative Jobs are
    # generated here.
    if ($apiDocuments -match '(?m)^\s*name:\s*commerce(?:-service)?\s*$') { $expectedJobs += 'commerce' }
    if ($apiDocuments -match '(?m)^\s*name:\s*content(?:-service)?\s*$') { $expectedJobs += 'content' }
    if ($apiDocuments -match '(?m)^\s*name:\s*manufacturing(?:-service)?\s*$') { $expectedJobs += 'manufacturing' }
    $missingJobs = @($expectedJobs | Where-Object { $jobNames -notcontains $_ })
    $invalidJobs = @($migrationJobs | Where-Object {
        $_ -notmatch '(?m)^\s*backoffLimit:\s*0\s*$' -or
        $_ -notmatch '(?m)^\s*activeDeadlineSeconds:\s*900\s*$' -or
        $_ -notmatch 'argocd.argoproj.io/hook:\s*Sync' -or
        $_ -notmatch 'Persistence__RunMigrationsOnStartup\s*\r?\n\s*value:\s*["'']?true' -or
        $_ -notmatch 'Persistence__MigrationOnly\s*\r?\n\s*value:\s*["'']?true' -or
        # Production currently uses the Kubernetes Vault auth role with a
        # projected Vault-audience service-account JWT.  Keep accepting the
        # legacy SPIFFE-JWT mode for overlays that still use it, but do not
        # weaken the remaining checks (static tokens remain forbidden and the
        # projected token/CA mounts are required below).
        $_ -notmatch 'Vault__AuthMethod\s*\r?\n\s*\s*value:\s*(?:kubernetes|spiffe-jwt)' -or
        $_ -notmatch 'Vault__AllowStaticToken\s*\r?\n\s*\s*value:\s*["'']?false' -or
        $_ -notmatch 'Redis__ConnectionString' -or
        $_ -notmatch 'Redis__TlsCaFile\s*\r?\n\s*\s*value:\s*/etc/tls/redis/ca.crt' -or
        $_ -notmatch 'secretName:\s*redis-tls' -or
        $_ -notmatch 'fsGroup:\s*1654' -or
        # Production accepts either Kubernetes Vault auth with a projected
        # service-account token, or the legacy SPIFFE-JWT bootstrap.  Both
        # paths remain fail-closed against static Vault tokens.
        (
            (($_ -match 'Vault__AuthMethod\s*\r?\n\s*\s*value:\s*kubernetes') -and
                $_ -notmatch 'serviceAccountToken:') -or
            (($_ -match 'Vault__AuthMethod\s*\r?\n\s*\s*value:\s*spiffe-jwt') -and
                (($_ -notmatch 'spire-jwt-fetcher') -or
                    ($_ -notmatch 'hostPath:\s*\r?\n\s*path:\s*/run/spire/sockets'))) -or
            (($_ -notmatch 'Vault__AuthMethod\s*\r?\n\s*\s*value:\s*(?:kubernetes|spiffe-jwt)'))
        )
    })
    $unpinnedJobImages = @($migrationJobs | ForEach-Object {
        [regex]::Matches($_, '(?m)^\s*image:\s*(?<image>\S+)') | ForEach-Object { $_.Groups['image'].Value }
    } | Where-Object { $_ -notmatch '@sha256:[0-9a-f]{64}$' })
    if ($missingJobs.Count -gt 0 -or $migrationJobs.Count -ne $expectedJobs.Count) {
        Add-Check 'migration-job-coverage' 'fail' "Expected seven rendered migration Jobs; found $($migrationJobs.Count), missing [$($missingJobs -join ',')]."
    } elseif ($invalidJobs.Count -gt 0) {
        Add-Check 'migration-job-contract' 'fail' "$($invalidJobs.Count) migration Job document(s) miss deadline, hook, Vault/SPIRE or one-shot controls."
    } elseif ($unpinnedJobImages.Count -gt 0) {
        Add-Check 'migration-job-contract' 'fail' "Migration Jobs contain unpinned images: $($unpinnedJobImages -join ', ')."
    } else {
        Add-Check 'migration-job-contract' 'pass' 'Seven digest-pinned Argo Sync wave-20 migration Jobs have bounded, SPIRE/Vault-backed one-shot controls.'
    }
}

$failed = @($checks | Where-Object status -eq 'fail')
$blocked = @($checks | Where-Object status -eq 'blocked')
$status = if ($failed.Count -gt 0) { 'fail' } elseif ($blocked.Count -gt 0) { 'blocked' } else { 'pass' }
$result = [pscustomobject]@{ status = $status; checks = @($checks); generatedAtUtc = [DateTime]::UtcNow.ToString('o') }
$json = $result | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $dir = Split-Path -Parent $OutputPath
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -eq 'fail') { exit 60 }
if ($status -eq 'blocked') { exit 60 }
exit 0
