CREATE TABLE clinicaldb.Encounters (
    EncounterId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    AppointmentId UUID,
    ProviderId STRING(36) NOT NULL,
    EncounterDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    EncounterType STRING(20) NOT NULL,
    Status STRING(20) NOT NULL DEFAULT 'IN_PROGRESS',
    ChiefComplaint STRING(1000),
    -- HPI (History of Present Illness) - Value Object inline columns
    HpiOnset STRING(500),
    HpiLocation STRING(500),
    HpiDuration STRING(200),
    HpiCharacteristics STRING(1000),
    HpiAggravatingFactors STRING(1000),
    HpiRelievingFactors STRING(1000),
    HpiPriorTreatments STRING(1000),
    -- VitalSigns - Value Object inline columns (OwnsOne)
    Temperature DECIMAL(5,2),
    HeartRate INT,
    RespiratoryRate INT,
    SystolicBP INT,
    DiastolicBP INT,
    OxygenSaturation DECIMAL(5,2),
    HeightCm DECIMAL(6,2),
    WeightKg DECIMAL(6,2),
    Bmi DECIMAL(5,2),
    -- Additional encounter fields
    Assessment STRING(5000),
    Plan STRING(5000),
    DiagnosisNotes STRING(5000),
    -- Audit
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    INDEX idx_encounters_patient (PatientId),
    INDEX idx_encounters_provider (ProviderId),
    INDEX idx_encounters_status (Status),
    INDEX idx_encounters_date (EncounterDate)
);

-- Diagnoses child table matching EF OwnsMany mapping (table: EncounterDiagnoses)
CREATE TABLE clinicaldb.EncounterDiagnoses (
    EncounterId UUID NOT NULL REFERENCES clinicaldb.Encounters(EncounterId) ON DELETE CASCADE,
    Id INT NOT NULL DEFAULT unique_row_id(),
    ConditionName STRING(500) NOT NULL,
    Icd10Code STRING(20) NOT NULL,
    IsPrimary BOOL NOT NULL DEFAULT false,
    Notes STRING(1000),
    PRIMARY KEY (EncounterId, Id)
);

-- Procedures child table matching EF OwnsMany mapping (table: EncounterProcedures)
CREATE TABLE clinicaldb.EncounterProcedures (
    EncounterId UUID NOT NULL REFERENCES clinicaldb.Encounters(EncounterId) ON DELETE CASCADE,
    Id INT NOT NULL DEFAULT unique_row_id(),
    ProcedureName STRING(500) NOT NULL,
    CptCode STRING(20) NOT NULL,
    PerformedDate TIMESTAMPTZ NOT NULL,
    Notes STRING(1000),
    PRIMARY KEY (EncounterId, Id)
);

-- Keep Prescriptions and ClinicalNotes for backward compat (referenced by encounterId)
CREATE TABLE clinicaldb.Prescriptions (
    EncounterId UUID NOT NULL REFERENCES clinicaldb.Encounters(EncounterId) ON DELETE CASCADE,
    PrescriptionId UUID NOT NULL DEFAULT gen_random_uuid(),
    MedicationName STRING(200) NOT NULL,
    Dosage STRING(100) NOT NULL,
    Frequency STRING(100),
    Route STRING(50),
    DurationDays INT,
    PrescribedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    PrescribedBy STRING(36),
    Instructions STRING(1000),
    IsActive BOOL DEFAULT true,
    PRIMARY KEY (EncounterId, PrescriptionId)
);

CREATE TABLE clinicaldb.ClinicalNotes (
    EncounterId UUID NOT NULL REFERENCES clinicaldb.Encounters(EncounterId) ON DELETE CASCADE,
    NoteId UUID NOT NULL DEFAULT gen_random_uuid(),
    NoteType STRING(50) NOT NULL,
    Content STRING NOT NULL,
    RecordedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    RecordedBy STRING(36),
    PRIMARY KEY (EncounterId, NoteId)
);

CREATE TABLE clinicaldb.OutboxMessages (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Type STRING(500) NOT NULL,
    Content JSONB NOT NULL,
    CorrelationId STRING(200),
    CausationId STRING(200),
    OccurredOn TIMESTAMPTZ NOT NULL DEFAULT now(),
    ProcessedOn TIMESTAMPTZ,
    Status STRING(50) NOT NULL DEFAULT 'Pending',
    Error STRING(1000),
    RetryCount INT DEFAULT 0,
    LastRetryOn TIMESTAMPTZ,
    LockExpiresAt TIMESTAMPTZ,
    INDEX idx_outbox_status_occurred (Status, OccurredOn)
);

-- Backward-compatibility for existing deployments: add VitalSigns inline columns
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS HpiOnset STRING(500);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS HpiLocation STRING(500);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS HpiDuration STRING(200);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS HpiCharacteristics STRING(1000);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS HpiAggravatingFactors STRING(1000);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS HpiRelievingFactors STRING(1000);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS HpiPriorTreatments STRING(1000);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS Temperature DECIMAL(5,2);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS HeartRate INT;
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS RespiratoryRate INT;
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS SystolicBP INT;
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS DiastolicBP INT;
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS OxygenSaturation DECIMAL(5,2);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS HeightCm DECIMAL(6,2);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS WeightKg DECIMAL(6,2);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS Bmi DECIMAL(5,2);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS Assessment STRING(5000);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS Plan STRING(5000);
ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS DiagnosisNotes STRING(5000);

ALTER TABLE clinicaldb.OutboxMessages ADD COLUMN IF NOT EXISTS CorrelationId STRING(200);
ALTER TABLE clinicaldb.OutboxMessages ADD COLUMN IF NOT EXISTS CausationId STRING(200);
ALTER TABLE clinicaldb.OutboxMessages ADD COLUMN IF NOT EXISTS OccurredOn TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE clinicaldb.OutboxMessages ADD COLUMN IF NOT EXISTS ProcessedOn TIMESTAMPTZ;
ALTER TABLE clinicaldb.OutboxMessages ADD COLUMN IF NOT EXISTS Status STRING(50) NOT NULL DEFAULT 'Pending';
ALTER TABLE clinicaldb.OutboxMessages ADD COLUMN IF NOT EXISTS LastRetryOn TIMESTAMPTZ;
ALTER TABLE clinicaldb.OutboxMessages ADD COLUMN IF NOT EXISTS LockExpiresAt TIMESTAMPTZ;
