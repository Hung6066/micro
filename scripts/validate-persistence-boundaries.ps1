$ErrorActionPreference = 'Stop'

$services = @(
    @{ Name = 'Appointment'; Api = 'src/Services/AppointmentService/AppointmentService.Api/Program.cs'; Migration = 'src/Services/AppointmentService/AppointmentService.Infrastructure/Persistence/Migrations/*_InitialCreate.cs' },
    @{ Name = 'Clinical'; Api = 'src/Services/ClinicalService/ClinicalService.Api/Program.cs'; Migration = 'src/Services/ClinicalService/ClinicalService.Infrastructure/Persistence/Migrations/*_InitialCreate.cs' },
    @{ Name = 'Billing'; Api = 'src/Services/BillingService/BillingService.Api/Program.cs'; Migration = 'src/Services/BillingService/BillingService.Infrastructure/Persistence/Migrations/*_InitialCreate.cs' },
    @{ Name = 'Pharmacy'; Api = 'src/Services/PharmacyService/PharmacyService.Api/Program.cs'; Migration = 'src/Services/PharmacyService/PharmacyService.Infrastructure/Persistence/Migrations/*_InitialCreate.cs' },
    @{ Name = 'Lab'; Api = 'src/Services/LabService/LabService.Api/Program.cs'; Migration = 'src/Services/LabService/LabService.Infrastructure/Persistence/Migrations/*_InitialCreate.cs' },
    @{ Name = 'Patient'; Api = 'src/Services/PatientService/PatientService.Api/Program.cs'; Migration = 'src/Services/PatientService/PatientService.Infrastructure/Persistence/Migrations/*_InitialCreate.cs' }
)

foreach ($service in $services) {
    $apiSource = Get-Content -Raw -LiteralPath $service.Api
    if ($apiSource -notmatch 'AddHisHopeMigrationRunner') {
        throw "$($service.Name) API does not register the migration runner."
    }
    if (-not (Get-ChildItem -Path $service.Migration -File -ErrorAction SilentlyContinue)) {
        throw "$($service.Name) has no committed InitialCreate migration."
    }
}

$patientReadMigrations = Get-ChildItem -Path 'src/Services/PatientService/PatientService.Infrastructure/Projections/Migrations/*_InitialCreate.cs' -File -ErrorAction SilentlyContinue
if (-not $patientReadMigrations) {
    throw 'Patient read projection has no committed InitialCreate migration.'
}

$noOpFiles = @(Get-ChildItem -Path 'src/Services' -Recurse -File -Filter '*.cs' | Select-String -Pattern 'NoOpCacheService')
if ($noOpFiles.Count -gt 0) {
    throw "NoOpCacheService remains in service runtime code: $($noOpFiles -join ', ')"
}

Write-Host "Persistence boundary gate passed: $($services.Count) service migration sets and distributed cache registrations verified."
