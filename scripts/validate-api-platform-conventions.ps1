[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$services = @(
    'AppointmentService', 'BillingService', 'ClinicalService', 'FhirGateway',
    'IdentityService', 'LabService', 'PatientService', 'PharmacyService'
)

function Read-Text([string] $relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required file missing: $relativePath" }
    return Get-Content -LiteralPath $path -Raw
}

foreach ($service in $services) {
    $app = Join-Path $repoRoot "src/Services/$service/$service.Application/DependencyInjection.cs"
    $api = Join-Path $repoRoot "src/Services/$service/$service.Api/Program.cs"
    $appText = Read-Text ($app.Substring($repoRoot.Length + 1))
    $apiText = Read-Text ($api.Substring($repoRoot.Length + 1))

    if ($appText -notmatch 'AddHisHopeValidation\(') {
        throw "$service Application is missing AddHisHopeValidation."
    }
    if ($appText -notmatch 'His\.Hope\.Validation\.ValidationBehavior') {
        throw "$service Application is missing the shared MediatR validation behavior."
    }
    if ($apiText -notmatch 'AddHisHopeServiceDefaults\(' -or $apiText -notmatch 'UseHisHopeServiceDefaults\(') {
        throw "$service API is missing the shared ServiceDefaults bootstrap."
    }
    if ($apiText -notmatch 'UseHisHopeValidationErrors\(' -and $apiText -notmatch 'UseHisHopeServiceDefaults\(') {
        throw "$service API is missing shared validation error middleware."
    }
    if ($apiText -notmatch 'MapHisHopeHealthEndpoints\(') {
        throw "$service API is missing standardized live/ready health endpoints."
    }
    if ($appText -match 'typeof\(ValidationBehaviour<') {
        throw "$service still registers a local validation behavior."
    }
}

Write-Host "API platform conventions passed for $($services.Count) services."
