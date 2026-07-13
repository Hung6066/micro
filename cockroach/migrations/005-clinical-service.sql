CREATE TABLE clinicaldb.Encounters (
    EncounterId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    AppointmentId UUID,
    ProviderId STRING(36) NOT NULL,
    FacilityId UUID,
    EncounterDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    EncounterType STRING(50) NOT NULL,
    Status STRING(20) NOT NULL DEFAULT 'InProgress',
    ChiefComplaint STRING(500),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    INDEX idx_encounters_patient (PatientId),
    INDEX idx_encounters_status (Status) WHERE Status = 'InProgress'
);

CREATE TABLE clinicaldb.VitalSigns (
    EncounterId UUID NOT NULL REFERENCES clinicaldb.Encounters(EncounterId) ON DELETE CASCADE,
    VitalSignId UUID NOT NULL DEFAULT gen_random_uuid(),
    BloodPressureSystolic INT,
    BloodPressureDiastolic INT,
    HeartRate INT,
    Temperature DECIMAL(4,1),
    RespiratoryRate INT,
    OxygenSaturation DECIMAL(4,1),
    Height DECIMAL(5,1),
    Weight DECIMAL(5,1),
    BMI DECIMAL(4,1),
    RecordedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    RecordedBy STRING(36),
    PRIMARY KEY (EncounterId, VitalSignId)
);

CREATE TABLE clinicaldb.Diagnoses (
    EncounterId UUID NOT NULL REFERENCES clinicaldb.Encounters(EncounterId) ON DELETE CASCADE,
    DiagnosisId UUID NOT NULL DEFAULT gen_random_uuid(),
    ICD10Code STRING(20) NOT NULL,
    Description STRING(500) NOT NULL,
    IsPrimary BOOL DEFAULT false,
    DiagnosisType STRING(50),
    Status STRING(20) DEFAULT 'Active',
    RecordedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    RecordedBy STRING(36),
    PRIMARY KEY (EncounterId, DiagnosisId)
);

CREATE TABLE clinicaldb.Procedures (
    EncounterId UUID NOT NULL REFERENCES clinicaldb.Encounters(EncounterId) ON DELETE CASCADE,
    ProcedureId UUID NOT NULL DEFAULT gen_random_uuid(),
    CPTCode STRING(20) NOT NULL,
    Description STRING(500),
    PerformedAt TIMESTAMPTZ,
    PerformedBy STRING(36),
    Notes STRING(2000),
    PRIMARY KEY (EncounterId, ProcedureId)
);

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
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    ProcessedAt TIMESTAMPTZ,
    Error STRING(2000),
    RetryCount INT DEFAULT 0,
    INDEX idx_outbox_unprocessed (ProcessedAt) WHERE ProcessedAt IS NULL
);
