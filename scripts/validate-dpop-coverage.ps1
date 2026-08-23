[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptDirectory '..')).Path
}

$resourcePrograms = @(
    'src/Services/AppointmentService/AppointmentService.Api/Program.cs',
    'src/Services/BillingService/BillingService.Api/Program.cs',
    'src/Services/ClinicalService/ClinicalService.Api/Program.cs',
    'src/Services/DatabaseContinuityService/DatabaseContinuityService.Api/Program.cs',
    'src/Services/FhirGateway/FhirGateway.Api/Program.cs',
    'src/Services/LabService/LabService.Api/Program.cs',
    'src/Services/PatientService/PatientService.Api/Program.cs',
    'src/Services/PharmacyService/PharmacyService.Api/Program.cs'
)

$requiredResourceContracts = @(
    'AddHisHopeJwtAuthentication',
    'UseDpopAuthorizationSchemeNormalization',
    'UseDpopAccessTokenValidation'
)

$violations = [System.Collections.Generic.List[string]]::new()
foreach ($relativePath in $resourcePrograms) {
    $path = Join-Path $RepositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $violations.Add("Missing resource service entrypoint: $relativePath")
        continue
    }

    $source = Get-Content -LiteralPath $path -Raw
    foreach ($contract in $requiredResourceContracts) {
        if (-not $source.Contains($contract)) {
            $violations.Add("${relativePath}: missing $contract")
        }
    }
}

$identityPipeline = Join-Path $RepositoryRoot 'src/Services/IdentityService/IdentityService.Api/Composition/IdentityServicePipelineExtensions.cs'
if (-not (Test-Path -LiteralPath $identityPipeline)) {
    $violations.Add('Missing IdentityServicePipelineExtensions.cs')
} else {
    $source = Get-Content -LiteralPath $identityPipeline -Raw
    foreach ($contract in @('UseDpopAuthorizationSchemeNormalization', 'UseDpopAccessTokenValidation')) {
        if (-not $source.Contains($contract)) {
            $violations.Add("Identity pipeline: missing $contract")
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Error ("DPoP coverage gate failed:`n- " + ($violations -join "`n- "))
    exit 1
}

Write-Host "DPoP coverage gate passed: all resource APIs and Identity pipeline enforce sender-constrained access tokens."
