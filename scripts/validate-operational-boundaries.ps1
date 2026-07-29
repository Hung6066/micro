$ErrorActionPreference = 'Stop'

$servicePrograms = @(
    'src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs',
    'src/Services/AppointmentService/AppointmentService.Api/Program.cs',
    'src/Services/BillingService/BillingService.Api/Program.cs',
    'src/Services/ClinicalService/ClinicalService.Api/Program.cs',
    'src/Services/LabService/LabService.Api/Program.cs',
    'src/Services/PatientService/PatientService.Api/Program.cs',
    'src/Services/PharmacyService/PharmacyService.Api/Program.cs'
)

foreach ($path in $servicePrograms) {
    if (-not (Test-Path $path)) { throw "Missing service composition file: $path" }
    $content = Get-Content $path -Raw
    if ($content -notmatch 'PhiDestructuringPolicy') {
        throw "PHI log redaction is not wired in $path"
    }
}

$dlqPath = 'src/Shared/Infrastructure/His.Hope.Infrastructure/Messaging/DeadLetterConsumer.cs'
$dlq = Get-Content $dlqPath -Raw
if ($dlq -match 'Body:\s*\{Body\}') { throw 'DLQ logging must not emit message body contents.' }
if ($dlq -notmatch 'BodyLength') { throw 'DLQ logging must retain body length evidence.' }

if (-not (Test-Path 'src/Shared/Infrastructure/His.Hope.Infrastructure/Degradation/StaleCacheFallbackPolicy.cs')) {
    throw 'Stale cache fallback policy is missing.'
}
if (-not (Test-Path 'src/Shared/Infrastructure/His.Hope.Infrastructure/Messaging/DeadLetterConsumer.cs')) {
    throw 'Dead-letter consumer is missing.'
}
if (-not (Test-Path 'k8s/monitoring/prometheus-rules.yaml')) {
    throw 'Prometheus operational alerts are missing.'
}
if (-not (Test-Path 'docs/operations/disaster-recovery.md')) {
    throw 'Disaster recovery runbook is missing.'
}

Write-Host "Operational boundary gate passed: PHI redaction, DLQ-safe logging, degradation, monitoring, and DR artifacts verified."
