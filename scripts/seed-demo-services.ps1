<#
  Seeds a linked, synthetic clinical graph for the local Docker compose databases.
  This is demo/test data only; it is not real patient data and must not be run
  against a production database. Every INSERT is keyed by a stable UUID.
#>

[CmdletBinding()]
param([switch]$IncludeClinicalData)

$ErrorActionPreference = 'Stop'

function Invoke-SeedSql {
    param([string]$Database, [string]$Sql)
    docker exec his-hope-postgres psql -U postgres -d $Database -v ON_ERROR_STOP=1 -c $Sql
    if ($LASTEXITCODE -ne 0) { throw "Seed failed for database $Database" }
}

$patientId = '22222222-2222-2222-2222-222222222222'
$providerId = '11111111-1111-1111-1111-111111111111'
$appointmentId = '33333333-3333-3333-3333-333333333333'
$encounterId = '44444444-4444-4444-4444-444444444444'
$labOrderId = '55555555-5555-5555-5555-555555555555'
$labTestId = '55555555-5555-5555-5555-555555555556'
$labResultId = '55555555-5555-5555-5555-555555555557'
$medicationId = '66666666-6666-6666-6666-666666666666'
$prescriptionId = '66666666-6666-6666-6666-666666666667'
$invoiceId = '77777777-7777-7777-7777-777777777777'
$lineItemId = '77777777-7777-7777-7777-777777777778'

if ($IncludeClinicalData) {
Invoke-SeedSql 'patientdb' @"
INSERT INTO patients (patient_id, facility_id, first_name, last_name, middle_name, date_of_birth, gender, phone, email, street, district, city, province, postal_code, country, blood_type, race, marital_status, insurance_id, national_id, occupation, emergency_contact_name, emergency_contact_phone, is_active, created_at, updated_at)
VALUES ('$patientId', 'demo-hospital', 'Nguyen', 'Minh An', 'Thi', '1988-04-12T00:00:00Z', 'F', '+84900000001', 'demo.patient@hishop.local', '12 Demo Street', 'District 1', 'Ho Chi Minh City', 'Ho Chi Minh', '700000', 'VN', 'O+', 'Kinh', 'MARRIED', 'DEMO-INS-0001', 'DEMO-NID-0001', 'Product manager', 'Nguyen Van Binh', '+84900000002', true, NOW(), NOW())
ON CONFLICT (patient_id) DO NOTHING;
"@

Invoke-SeedSql 'appointmentdb' @"
INSERT INTO appointments (appointment_id, facility_id, "PatientId", "ProviderId", "ScheduledDate", "StartTime", "EndTime", "Status", "Type", "Reason", "Notes", "Location", "CreatedAt", "UpdatedAt")
VALUES ('$appointmentId', 'demo-hospital', '$patientId', '$providerId', NOW() + INTERVAL '1 day', INTERVAL '09:00', INTERVAL '09:30', 'SCHEDULED', 'CLINIC', 'Routine follow-up', 'Synthetic demo appointment', 'Clinic A - Room 101', NOW(), NOW())
ON CONFLICT (appointment_id) DO NOTHING;
"@

Invoke-SeedSql 'clinicaldb' @"
INSERT INTO encounters (encounter_id, facility_id, "PatientId", "ProviderId", "AppointmentId", "EncounterDate", "EncounterType", "ChiefComplaint", "Assessment", "Plan", "DiagnosisNotes", "Status", created_at, updated_at)
VALUES ('$encounterId', 'demo-hospital', '$patientId', '$providerId', '$appointmentId', NOW(), 'OUTPATIENT', 'Routine health review', 'Stable synthetic demo patient', 'Continue observation and follow-up in 3 months', 'Demo data; no clinical decision value', 'IN_PROGRESS', NOW(), NOW())
ON CONFLICT (encounter_id) DO NOTHING;
"@

Invoke-SeedSql 'labdb' @"
INSERT INTO "LabOrders" (id, facilityid, patientid, providerid, encounterid, orderdate, status, priority, notes, createdat, updatedat)
VALUES ('$labOrderId', 'demo-hospital', '$patientId', '$providerId', '$encounterId', NOW(), 'ORDERED', 'ROUTINE', 'Synthetic demo order', NOW(), NOW())
ON CONFLICT (id) DO NOTHING;
INSERT INTO "LabTests" (id, laborderid, testcode, testname, specimentype, status, orderedat, createdat, updatedat)
VALUES ('$labTestId', '$labOrderId', 'CBC', 'Complete blood count', 'Blood', 'COMPLETED', NOW(), NOW(), NOW())
ON CONFLICT (id) DO NOTHING;
INSERT INTO "LabResults" (id, labresultid, value, unit, referencerange, abnormalflag, resultstatus, resultedat, performedby, notes, labtestid)
VALUES ('$labResultId', '$labResultId', '13.8', 'g/dL', '12.0-16.0', 'NORMAL', 'FINAL', NOW(), 'Demo laboratory', 'Synthetic result', '$labTestId')
ON CONFLICT (id) DO NOTHING;
"@

Invoke-SeedSql 'pharmacydb' @"
INSERT INTO "Medications" (id, facilityid, name, genericname, brandname, dosageform, strength, route, category, manufacturer, requiresprescription, isactive, createdat, updatedat)
VALUES ('$medicationId', 'demo-hospital', 'Demo Amoxicillin', 'Amoxicillin', 'DemoMox', 'CAPSULE', '500 mg', 'ORAL', 'Antibiotic', 'Synthetic Pharma', true, true, NOW(), NOW())
ON CONFLICT (id) DO NOTHING;
INSERT INTO "Prescriptions" (id, facilityid, patientid, providerid, medicationid, medicationname, strength, dosageform, dosageinstructions, route, quantity, refills, notes, status, prescribeddate, expirydate, createdat, updatedat)
VALUES ('$prescriptionId', 'demo-hospital', '$patientId', '$providerId', '$medicationId', 'Demo Amoxicillin', '500 mg', 'CAPSULE', 'Take one capsule twice daily with food', 'ORAL', 14, 0, 'Synthetic demo prescription', 'PRESCRIBED', NOW(), NOW() + INTERVAL '30 days', NOW(), NOW())
ON CONFLICT (id) DO NOTHING;
"@

Invoke-SeedSql 'billingdb' @"
INSERT INTO billing."Invoices" ("Id", "FacilityId", "PatientId", "EncounterId", "InvoiceNumber", "InvoiceDate", "DueDate", "Status", "Notes", "SubTotal", "TaxAmount", "DiscountAmount", "PaidAmount", "CreatedAt", "UpdatedAt")
VALUES ('$invoiceId', 'demo-hospital', '$patientId', '$encounterId', 'DEMO-INV-0001', NOW(), NOW() + INTERVAL '30 days', 'SUBMITTED', 'Synthetic demo invoice', 100.00, 10.00, 0.00, 0.00, NOW(), NOW())
ON CONFLICT ("Id") DO NOTHING;
INSERT INTO billing."InvoiceLineItems" ("Id", "InvoiceId", "Description", "Quantity", "UnitPrice", "ItemCode", "ItemType", "CreatedAt")
VALUES ('$lineItemId', '$invoiceId', 'Outpatient consultation', 1, 100.00, 'CONSULT', 'SERVICE', NOW())
ON CONFLICT ("Id") DO NOTHING;
"@
}

if ($IncludeClinicalData) {
    Write-Host 'Synthetic linked clinical graph seeded successfully.' -ForegroundColor Green
    Write-Host "Patient: $patientId | Appointment: $appointmentId | Encounter: $encounterId | Lab: $labOrderId | Prescription: $prescriptionId | Invoice: $invoiceId"
} else {
    Write-Host 'IAM/workflow seed is owned by IdentityDbInitializer. Clinical fixture was skipped.' -ForegroundColor Green
}
