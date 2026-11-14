[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$Root = if ([string]::IsNullOrWhiteSpace($Root)) {
    (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
} else {
    (Resolve-Path $Root).Path
}
$sourceRoot = Join-Path $Root 'src'
$sourceFiles = Get-ChildItem $sourceRoot -Recurse -Include '*.cs' -File |
    Where-Object { $_.FullName -notmatch '[\\/]?(bin|obj)[\\/]' }

function Assert-NoMatch([string]$Pattern, [string]$Description, [string[]]$Exclude = @()) {
    $excludedPaths = @($Exclude | ForEach-Object { [IO.Path]::GetFullPath($_) })
    $matches = @($sourceFiles | Where-Object { $excludedPaths -notcontains $_.FullName } |
        Select-String -Pattern $Pattern)
    if ($matches.Count -gt 0) {
        $locations = ($matches | Select-Object -First 10 | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join ', '
        throw "${Description}: $locations"
    }
}

$grpcHelper = Join-Path $Root 'src/Shared/Configuration/His.Hope.Configuration/GrpcClientRegistrationExtensions.cs'
$rabbitHelper = Join-Path $Root 'src/Shared/Infrastructure/His.Hope.Infrastructure/Messaging/RabbitMqCompatibilityExtensions.cs'
$serviceAuth = Join-Path $Root 'src/Shared/Configuration/His.Hope.Configuration/ServiceToServiceAuthentication.cs'
$rabbitImplementation = Join-Path $Root 'src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/Implementations/EventBusServiceExtensions.cs'

foreach ($required in @($grpcHelper, $rabbitHelper, $serviceAuth)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required shared transport file missing: $required" }
}

$grpcText = Get-Content -LiteralPath $grpcHelper -Raw
$authText = Get-Content -LiteralPath $serviceAuth -Raw
if ($grpcText -notmatch 'AddCallCredentials') { throw 'Shared gRPC registration must attach call credentials.' }
if ($authText -notmatch 'PropagateUserToken' -or $authText -notmatch 'client_credentials') {
    throw 'Shared service-to-service authentication must support request token propagation and client credentials.'
}

Assert-NoMatch 'AddGrpcClient\s*<' 'Direct AddGrpcClient registration found outside the shared gRPC helper.' @($grpcHelper)
Assert-NoMatch 'AddRabbitMQEventBus\s*\(' 'Direct RabbitMQ legacy registration found outside the compatibility helper.' @($rabbitHelper, $rabbitImplementation)

$servicePrograms = Get-ChildItem (Join-Path $Root 'src/Services') -Recurse -Filter 'Program.cs' -File |
    Where-Object { $_.FullName -notmatch '[\\/]?(bin|obj)[\\/]' }
$platformServices = @('IdentityService', 'ManufacturingService', 'ContentService', 'CommerceService', 'AppointmentService', 'BillingService', 'ClinicalService', 'LabService', 'PatientService', 'PharmacyService', 'FhirGateway')
foreach ($service in $platformServices) {
    $program = $servicePrograms | Where-Object { $_.FullName -match "[\\/]$service[\\/]" } | Select-Object -First 1
    if ($null -eq $program) { throw "Program.cs not found for $service." }
    $text = Get-Content -LiteralPath $program.FullName -Raw
    if ($text -notmatch 'AddHisHopeServicePlatform\(' -and $text -notmatch 'AddIdentityService\(' -and $text -notmatch 'AddCommerceServiceHost\(') {
        throw "$service does not bootstrap the shared service platform."
    }
}

Write-Host "Transport standardization passed: $($platformServices.Count) services, shared gRPC auth, RabbitMQ registration and token contract verified."
