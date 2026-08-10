[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$MigrationDirectory = 'artifacts/database-migrations-current',
    [string]$RenderedProductionManifest = 'artifacts/k8s/prod.yaml',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
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
        $actual = (Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $entry.sha256.ToLowerInvariant()) { $hashFailures.Add("hash:$($entry.script)") }
    }
    if ($hashFailures.Count -gt 0) { Add-Check 'migration-artifact-integrity' 'fail' ($hashFailures -join ', ') }
    else { Add-Check 'migration-artifact-integrity' 'pass' 'All migration scripts match the signed-off manifest hashes.' }
}

$sqlFiles = @(Get-ChildItem -LiteralPath $directory -Filter '*-idempotent.sql' -File -ErrorAction SilentlyContinue)
$destructive = [System.Collections.Generic.List[string]]::new()
foreach ($file in $sqlFiles) {
    $matches = Select-String -LiteralPath $file.FullName -Pattern '(?i)\b(drop\s+(table|column|schema)|truncate\s+table|delete\s+from)\b' -AllMatches
    if ($matches) { $destructive.Add($file.Name) }
}
if ($destructive.Count -gt 0) { Add-Check 'destructive-migration-review' 'blocked' "Destructive SQL requires an explicit expand/contract review: $($destructive -join ', ')" }
else { Add-Check 'destructive-migration-review' 'pass' 'No destructive SQL pattern detected in generated scripts.' }

# Every API that owns an EF context must expose a one-shot migration-only mode
# for the reviewed Kubernetes Job. This keeps DDL out of long-running API
# replicas while preserving the existing development convenience path.
$migrationOnlySources = @(
    'src/Services/IdentityService/IdentityService.Api/Program.cs',
    'src/Services/IdentityService/IdentityService.Infrastructure/Persistence/IdentityDbInitializer.cs',
    'src/Services/AppointmentService/AppointmentService.Api/Program.cs',
    'src/Services/ClinicalService/ClinicalService.Api/Program.cs',
    'src/Services/LabService/LabService.Api/Program.cs',
    'src/Services/BillingService/BillingService.Api/Program.cs',
    'src/Services/PatientService/PatientService.Api/Program.cs',
    'src/Services/PharmacyService/PharmacyService.Api/Program.cs'
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
    Add-Check 'migration-only-runner' 'pass' 'All eight EF owners expose a migration-only exit path.'
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
    $missingJobs = @($expectedJobs | Where-Object { $jobNames -notcontains $_ })
    $invalidJobs = @($migrationJobs | Where-Object {
        $_ -notmatch '(?m)^\s*backoffLimit:\s*0\s*$' -or
        $_ -notmatch '(?m)^\s*activeDeadlineSeconds:\s*900\s*$' -or
        $_ -notmatch 'argocd.argoproj.io/hook:\s*Sync' -or
        $_ -notmatch 'Persistence__RunMigrationsOnStartup\s*\r?\n\s*value:\s*["'']?true' -or
        $_ -notmatch 'Persistence__MigrationOnly\s*\r?\n\s*value:\s*["'']?true' -or
        $_ -notmatch 'Vault__AuthMethod\s*\r?\n\s*\s*value:\s*spiffe-jwt' -or
        $_ -notmatch 'Vault__AllowStaticToken\s*\r?\n\s*\s*value:\s*["'']?false' -or
        $_ -notmatch 'Redis__ConnectionString' -or
        $_ -notmatch 'Redis__TlsCaFile\s*\r?\n\s*\s*value:\s*/etc/tls/redis/ca.crt' -or
        $_ -notmatch 'secretName:\s*redis-tls' -or
        $_ -notmatch 'fsGroup:\s*1654' -or
        $_ -notmatch 'spire-jwt-fetcher' -or
        $_ -notmatch 'hostPath:\s*\r?\n\s*path:\s*/run/spire/sockets'
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
