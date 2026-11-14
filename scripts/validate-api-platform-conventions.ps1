[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$services = @(
    'AppointmentService', 'BillingService', 'ClinicalService', 'ContentService',
    'CommerceService', 'DatabaseContinuityService', 'ExternalIntegrationService',
    'FhirGateway', 'IdentityService', 'LabService', 'ManufacturingService',
    'PatientService', 'PharmacyService'
)

function Read-Text([string] $relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required file missing: $relativePath" }
    return Get-Content -LiteralPath $path -Raw
}

foreach ($service in $services) {
    $app = Get-ChildItem (Join-Path $repoRoot "src/Services/$service") -Recurse -Filter 'DependencyInjection.cs' -File |
        Select-Object -First 1 -ExpandProperty FullName
    $apiProjectRoot = Join-Path $repoRoot "src/Services/$service/$service.Api"
    $api = Join-Path $apiProjectRoot 'Program.cs'
    $appText = if ($app) { Get-Content -LiteralPath $app -Raw } else { '' }
    $apiText = Read-Text ($api.Substring($repoRoot.Length + 1))
    # Host composition is intentionally kept out of Program.cs for services
    # with security/data-protection setup. Validate the complete API project so
    # a composed Add*/Use* host cannot be mistaken for a missing platform hook.
    $apiCompositionText = (Get-ChildItem -LiteralPath $apiProjectRoot -Recurse -Filter '*.cs' -File |
        Where-Object { $_.FullName -notmatch '[\\/]obj[\\/]|[\\/]bin[\\/]' } |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
    $apiValidationText = "$apiText`n$apiCompositionText"

    $hasApplicationLayer = -not [string]::IsNullOrWhiteSpace($app)
    $requiresMediatRValidation = $hasApplicationLayer -and $appText -match 'AddMediatR\('
    if ($requiresMediatRValidation -and $appText -notmatch 'AddHisHopeValidation\(') {
        throw "$service Application is missing AddHisHopeValidation."
    }
    if ($requiresMediatRValidation -and $appText -notmatch 'His\.Hope\.Validation\.ValidationBehavior') {
        throw "$service Application is missing the shared MediatR validation behavior."
    }
    $hasServiceDefaultsBootstrap = $apiValidationText -match 'AddHisHopeServicePlatform\(' -or $apiValidationText -match 'AddHisHopeServiceDefaults\(' -or $apiValidationText -match 'AddIdentityService\('
    $hasServiceDefaultsPipeline = $apiValidationText -match 'UseHisHopeServiceDefaults\(' -or $apiValidationText -match 'UseIdentityServicePipeline\('
    if (-not $hasServiceDefaultsBootstrap -or -not $hasServiceDefaultsPipeline) {
        throw "$service API is missing the shared ServiceDefaults bootstrap."
    }
    if ($apiValidationText -notmatch 'UseHisHopeValidationErrors\(' -and $apiValidationText -notmatch 'UseHisHopeServiceDefaults\(' -and $apiValidationText -notmatch 'UseIdentityServicePipeline\(') {
        throw "$service API is missing shared validation error middleware."
    }
    if ($apiValidationText -notmatch 'MapHisHopeHealthEndpoints\(') {
        throw "$service API is missing standardized live/ready health endpoints."
    }
    if ($requiresMediatRValidation -and $appText -match 'typeof\(ValidationBehaviour<') {
        throw "$service still registers a local validation behavior."
    }
}

Write-Host "API platform conventions passed for $($services.Count) services."
