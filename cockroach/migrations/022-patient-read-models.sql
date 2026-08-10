-- CQRS event-sourced read models for patient queries
-- Denormalized for fast reads — updated by PatientProjector via integration events

CREATE TABLE patientdb.PatientReadModels (
    PatientId UUID PRIMARY KEY,
    FullName STRING(200) NOT NULL,
    DateOfBirth DATE NOT NULL,
    Gender STRING(10) NOT NULL,
    PrimaryDiagnosis STRING(500),
    LastVisitDate TIMESTAMPTZ,
    EncounterCount INT NOT NULL DEFAULT 0,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    INDEX idx_patient_read_models_last_visit (LastVisitDate),
    INDEX idx_patient_read_models_full_name (FullName)
);

-- Backward-compatibility ALTER TABLE for existing deployments
ALTER TABLE patientdb.PatientReadModels ADD COLUMN IF NOT EXISTS FullName STRING(200);
ALTER TABLE patientdb.PatientReadModels ADD COLUMN IF NOT EXISTS DateOfBirth DATE;
ALTER TABLE patientdb.PatientReadModels ADD COLUMN IF NOT EXISTS Gender STRING(10);
ALTER TABLE patientdb.PatientReadModels ADD COLUMN IF NOT EXISTS PrimaryDiagnosis STRING(500);
ALTER TABLE patientdb.PatientReadModels ADD COLUMN IF NOT EXISTS LastVisitDate TIMESTAMPTZ;
ALTER TABLE patientdb.PatientReadModels ADD COLUMN IF NOT EXISTS EncounterCount INT;
ALTER TABLE patientdb.PatientReadModels ADD COLUMN IF NOT EXISTS CreatedAt TIMESTAMPTZ;
ALTER TABLE patientdb.PatientReadModels ADD COLUMN IF NOT EXISTS UpdatedAt TIMESTAMPTZ;
