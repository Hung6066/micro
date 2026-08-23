[CmdletBinding()]
param(
    [ValidateSet('phase1', 'phase2', 'phase3', 'all')]
    [string]$Phase = 'all',
    [string]$RepositoryRoot,
    [switch]$SkipIntegrationTests,
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
        Add-Check $Name 'fail' $_.Exception.Message
        throw
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
        Invoke-Step 'siem-tamper-drill' { ./scripts/run-audit-siem-tamper-drill.ps1 }
        if (Test-Path -LiteralPath 'artifacts/security/penetration-test/report.json') {
            Invoke-Step 'pentest-evidence' { ./scripts/verify-independent-security-evidence.ps1 -EvidenceRoot artifacts/security }
        } else {
            Add-Check 'pentest-evidence' 'skipped' 'External penetration-test report not present in repository.'
        }
    }

    if ($Phase -in @('phase2', 'all')) {
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
        if (Test-Path -LiteralPath 'tests/load/results/baseline-summary.json') {
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
$result = [pscustomobject]@{
    status = if ($failed.Count -gt 0) { 'fail' } else { 'pass' }
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    phase = $Phase
    checks = @($checks)
}
$json = $result | ConvertTo-Json -Depth 6
New-Item -ItemType Directory -Force -Path (Join-Path $RepositoryRoot 'artifacts/evidence') | Out-Null
[IO.File]::WriteAllText((Join-Path $RepositoryRoot 'artifacts/evidence/enterprise-production-phases.json'), $json, [Text.UTF8Encoding]::new($false))
Write-Output $json
if ($failed.Count -gt 0) { exit 80 }
exit 0
