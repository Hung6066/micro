$ErrorActionPreference = 'Stop'
$builds = @(
    @{ Name = 'appointment-service'; File = 'src/Services/AppointmentService/AppointmentService.Api/Dockerfile' },
    @{ Name = 'billing-service'; File = 'src/Services/BillingService/BillingService.Api/Dockerfile' },
    @{ Name = 'clinical-service'; File = 'src/Services/ClinicalService/ClinicalService.Api/Dockerfile' },
    @{ Name = 'identity-service'; File = 'src/Services/IdentityService/IdentityService.Api/Dockerfile' },
    @{ Name = 'lab-service'; File = 'src/Services/LabService/LabService.Api/Dockerfile' },
    @{ Name = 'patient-service'; File = 'src/Services/PatientService/PatientService.Api/Dockerfile' },
    @{ Name = 'pharmacy-service'; File = 'src/Services/PharmacyService/PharmacyService.Api/Dockerfile' }
)
foreach ($build in $builds) {
    Write-Host "BUILD $($build.Name)"
    & rtk docker build -t "his-hope/$($build.Name):latest" -f $build.File .
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
