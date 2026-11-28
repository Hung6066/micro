[CmdletBinding()]
param(
    [ValidateSet('phase1', 'phase2', 'phase3', 'all')]
    [string]$Phase = 'all',
    [string]$RepositoryRoot,
    [string]$IntegrationMatrixPath = 'artifacts/evidence/integration-matrix/integration-test-matrix.json',
    [int]$IntegrationMatrixMaxAgeHours = 24,
    [switch]$SkipIntegrationTests,
    [switch]$SkipServiceIntegrationMatrix,
    [switch]$SkipLoadTestBaseline,
    [switch]$AllowLocalDrEvidence
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [string]$Status, [string]$Message) {
    $checks.Add([pscustomobject]@{ phase = $Phase; name = $Name; status = $Status; message = $Message })
}

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    try {
        & $Action
        Add-Check $Name 'pass' 'Completed successfully.'
    } catch {
        $message = $_.Exception.Message
        $status = 'fail'
        if ($Name -eq 'load-test-baseline' -and
            $message -match 'summary missing|no HTTP requests|AUTH_TOKEN is required') {
            $status = 'environment-blocked'
        }

        # Keep collecting the remaining phase checks so `-Phase all` produces
        # a complete evidence matrix. The aggregate result below remains
        # fail-closed for both failures and environment blockers.
        Add-Check $Name $status $message
    }
}

Push-Location $RepositoryRoot
try {
    if ($Phase -in @('phase1', 'all')) {
        Invoke-Step 'dpop-coverage' { ./scripts/validate-dpop-coverage.ps1 -RepositoryRoot $RepositoryRoot }
        if (-not $SkipIntegrationTests) {
            Invoke-Step 'rfc9700-matrix' { ./scripts/generate-rfc9700-conformance-report.ps1 -RepositoryRoot $RepositoryRoot }
        } else {
            Add-Check 'rfc9700-matrix' 'skipped' 'Integration tests skipped by flag.'
        }
        if ($SkipServiceIntegrationMatrix) {
            Add-Check 'service-integration-matrix' 'skipped' 'Service integration matrix skipped by flag.'
        } else {
            $matrixPath = if ([IO.Path]::IsPathRooted($IntegrationMatrixPath)) {
                $IntegrationMatrixPath
            } else {
                Join-Path $RepositoryRoot $IntegrationMatrixPath
            }
            if (-not (Test-Path -LiteralPath $matrixPath -PathType Leaf)) {
                Add-Check 'service-integration-matrix' 'environment-blocked' "Integration matrix evidence is missing: $IntegrationMatrixPath"
            } else {
                $matrix = Get-Content -LiteralPath $matrixPath -Raw | ConvertFrom-Json
                $matrixFailed = if ($null -ne $matrix.totals) { [int]$matrix.totals.failed } else { 0 }
                $matrixSkipped = if ($null -ne $matrix.totals) { [int]$matrix.totals.skipped } else { 0 }
                $matrixGeneratedAt = [DateTimeOffset]::MinValue
                $matrixTimestampValid = $false
                try {
                    $matrixGeneratedAt = [DateTimeOffset]::Parse([string]$matrix.generatedAtUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal)
                    $matrixTimestampValid = $true
                } catch {
                    $matrixTimestampValid = $false
                }
                $matrixAgeHours = if ($matrixTimestampValid) { ([DateTimeOffset]::UtcNow - $matrixGeneratedAt).TotalHours } else { [double]::PositiveInfinity }
                if ($matrixFailed -gt 0) {
                    Add-Check 'service-integration-matrix' 'fail' "Integration matrix contains $matrixFailed failed tests."
                } elseif (-not $matrixTimestampValid -or $matrixAgeHours -gt $IntegrationMatrixMaxAgeHours) {
                    Add-Check 'service-integration-matrix' 'environment-blocked' "Integration matrix evidence is stale or has no valid generatedAtUtc: ageHours=$([math]::Round($matrixAgeHours, 2)), maxAgeHours=$IntegrationMatrixMaxAgeHours."
                } elseif ($matrixSkipped -gt 0 -or [string]$matrix.status -ne 'pass') {
                    Add-Check 'service-integration-matrix' 'environment-blocked' "Integration matrix is not green: status=$($matrix.status), skipped=$matrixSkipped."
                } else {
                    Add-Check 'service-integration-matrix' 'pass' 'All service integration tests passed.'
                }
            }
        }
        if ($AllowLocalDrEvidence) {
            Invoke-Step 'dr-evidence-local' { ./scripts/run-local-dr-evidence-drill.ps1 }
        }
        Invoke-Step 'dr-evidence-contract' {
            if ($AllowLocalDrEvidence) {
                ./scripts/validate-dr-evidence.ps1 -EvidenceDirectory artifacts/evidence -OutputPath artifacts/evidence/enterprise-phase1-dr.json
            } else {
                ./scripts/validate-dr-evidence.ps1 -StaticOnly -OutputPath artifacts/evidence/enterprise-phase1-dr.json
            }
        }
        Invoke-Step 'production-release-contracts' {
            foreach ($vaultConfigPath in @('vault/config.hcl', 'k8s/vault/vault-statefulset.yaml')) {
                $vaultConfig = Get-Content -LiteralPath $vaultConfigPath -Raw
                if ($vaultConfig -match 'secret_shreshold') {
                    throw "Vault config contains the invalid Shamir option secret_shreshold: $vaultConfigPath"
                }
                if ($vaultConfig -notmatch '(?m)^\s*secret_threshold\s*=\s*3\s*$') {
                    throw "Vault config must declare secret_threshold = 3: $vaultConfigPath"
                }
            }
            ./scripts/validate-kustomize-release.ps1 -Environment prod
            ./scripts/config/validate-kustomize-runtime.ps1 -Overlay prod
            python scripts/validate-production-ha-contract.py
            python scripts/validate-production-data-plane-ha-contract.py
            ./scripts/validate-signature-controller-contract.ps1
            ./scripts/validate-observability-contract.ps1 -OutputPath artifacts/evidence/observability-contract.json
            ./scripts/validate-observability-production.ps1
        }
        Invoke-Step 'siem-tamper-drill' { ./scripts/run-audit-siem-tamper-drill.ps1 }
        $oidcEvidencePath = 'artifacts/security/oidc-conformance/report.json'
        $pentestEvidencePath = 'artifacts/security/penetration-test/report.json'
        $hasExternalEvidence = $false
        if ((Test-Path -LiteralPath $oidcEvidencePath) -and (Test-Path -LiteralPath $pentestEvidencePath)) {
            $oidcEvidence = Get-Content -LiteralPath $oidcEvidencePath -Raw | ConvertFrom-Json
            $pentestEvidence = Get-Content -LiteralPath $pentestEvidencePath -Raw | ConvertFrom-Json
            $oidcSource = if ($null -ne $oidcEvidence.PSObject.Properties['evidenceSource']) { [string]$oidcEvidence.evidenceSource } else { '' }
            $pentestSource = if ($null -ne $pentestEvidence.PSObject.Properties['evidenceSource']) { [string]$pentestEvidence.evidenceSource } else { '' }
            $hasExternalEvidence = $oidcSource -eq 'external-independent' -and
                $pentestSource -eq 'external-independent'
        }
        if ($hasExternalEvidence) {
            Invoke-Step 'pentest-evidence' { ./scripts/verify-independent-security-evidence.ps1 -EvidenceRoot artifacts/security }
        } else {
            Add-Check 'pentest-evidence' 'environment-blocked' 'Local/automated evidence is present, but signed external-independent OIDC and penetration-test reports are required.'
        }
    }

    if ($Phase -in @('phase2', 'all')) {
        Invoke-Step 'tenant-context-contract' {
            ./scripts/verify-tenant-context-contract.ps1 -Root $RepositoryRoot
        }
        Invoke-Step 'threat-model-catalog' {
            python scripts/validate-threat-model.py
        }
        Invoke-Step 'assurance-policy-config' {
            if (-not (Test-Path -LiteralPath 'config/assurance-policy.v1.json')) { throw 'Missing assurance policy config.' }
        }
        Invoke-Step 'assurance-policy-tests' {
            dotnet test tests/Services/IdentityService/IdentityService.Application.Tests/IdentityService.Application.Tests.csproj `
                --configuration Release `
                --filter "FullyQualifiedName~AssurancePolicyEvaluatorTests" | Out-Host
            if ($LASTEXITCODE -ne 0) { throw 'Assurance policy tests failed.' }
        }
        Invoke-Step 'staging-device-posture-contract' {
            $staging = Get-Content -LiteralPath 'config/environments/staging.env.example' -Raw
            if ($staging -notmatch 'DEVICE_POSTURE_MODE=stepup') { throw 'Staging device posture must use stepup mode.' }
            if ($staging -notmatch 'DEVICE_POSTURE_ENFORCE_CLINICAL=true') { throw 'Staging clinical enforcement must be enabled.' }
            if ($staging -notmatch 'AUTHZ_PDP_MODE=canary') { throw 'Staging OpenFGA canary mode must be enabled.' }
        }
        Invoke-Step 'jwks-rotation-drill' { ./scripts/run-jwks-rotation-drill.ps1 }
        Invoke-Step 'load-profile-contract' { ./scripts/validate-load-profiles.ps1 -Root $RepositoryRoot }
        if ($SkipLoadTestBaseline) {
            Add-Check 'load-test-baseline' 'skipped' 'Load baseline skipped by operator request.'
        } elseif (Test-Path -LiteralPath 'tests/load/results/baseline-summary.json') {
            Invoke-Step 'load-test-baseline' { ./scripts/validate-load-test-baseline.ps1 }
        } else {
            Add-Check 'load-test-baseline' 'skipped' 'k6 baseline summary not present; run tests/Load/baseline-load-test.js locally.'
        }
    }

    if ($Phase -in @('phase3', 'all')) {
        Invoke-Step 'fapi-profile-tests' {
            dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj `
                --configuration Release `
                --filter "FullyQualifiedName~FapiSecurityProfile" | Out-Host
            if ($LASTEXITCODE -ne 0) { throw 'FAPI profile tests failed.' }
        }
        Invoke-Step 'scim-multi-vendor-tests' {
            dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj `
                --configuration Release `
                --filter "FullyQualifiedName~ScimMultiVendorConformanceTests" | Out-Host
            if ($LASTEXITCODE -ne 0) { throw 'SCIM multi-vendor tests failed.' }
        }
        Invoke-Step 'multi-region-overlay' {
            if (-not (Test-Path -LiteralPath 'k8s/overlays/multi-region/kustomization.yaml')) {
                throw 'Missing multi-region DR overlay.'
            }
            kubectl kustomize k8s/overlays/multi-region --load-restrictor LoadRestrictionsNone | Out-Null
        }
        Invoke-Step 'legacy-auth-deprecation' {
            dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj `
                --configuration Release `
                --filter "FullyQualifiedName~LegacyEndpoints_HaveDeprecationHeaders" | Out-Host
            if ($LASTEXITCODE -ne 0) { throw 'Legacy auth deprecation headers missing.' }
        }
    }
} finally {
    Pop-Location
}

$failed = @($checks | Where-Object status -eq 'fail')
$blocked = @($checks | Where-Object status -in @('environment-blocked', 'skipped'))
$result = [pscustomobject]@{
    # A production phase is not green when a required external evidence gate
    # was skipped. Keep the individual check as `skipped` for operator clarity,
    # but make the aggregate fail closed as `environment-blocked`.
    status = if ($failed.Count -gt 0) { 'fail' } elseif ($blocked.Count -gt 0) { 'environment-blocked' } else { 'pass' }
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    phase = $Phase
    checks = @($checks)
}
$json = $result | ConvertTo-Json -Depth 6
New-Item -ItemType Directory -Force -Path (Join-Path $RepositoryRoot 'artifacts/evidence') | Out-Null
[IO.File]::WriteAllText((Join-Path $RepositoryRoot 'artifacts/evidence/enterprise-production-phases.json'), $json, [Text.UTF8Encoding]::new($false))
Write-Output $json
if ($failed.Count -gt 0) { exit 80 }
if ($blocked.Count -gt 0) { exit 70 }
exit 0
