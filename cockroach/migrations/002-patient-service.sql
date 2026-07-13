-- Patient aggregate
CREATE TABLE patientdb.Patients (
    PatientId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    FullName STRING(200) NOT NULL,
    DateOfBirth DATE NOT NULL,
    Gender STRING(10) NOT NULL,
    BloodType STRING(5),
    Race STRING(50),
    MaritalStatus STRING(20),
    Phone STRING(20) NOT NULL,
    Email STRING(100),
    Address STRING(500),
    IsActive BOOL NOT NULL DEFAULT true,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    INDEX idx_patients_active (IsActive) STORING (FullName, Phone)
);

CREATE TABLE patientdb.Allergies (
    PatientId UUID NOT NULL REFERENCES patientdb.Patients(PatientId) ON DELETE CASCADE,
    AllergyId UUID NOT NULL DEFAULT gen_random_uuid(),
    Allergen STRING(200) NOT NULL,
    Reaction STRING(500),
    Severity STRING(20),
    RecordedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (PatientId, AllergyId)
);

CREATE TABLE patientdb.MedicalConditions (
    PatientId UUID NOT NULL REFERENCES patientdb.Patients(PatientId) ON DELETE CASCADE,
    ConditionId UUID NOT NULL DEFAULT gen_random_uuid(),
    ConditionName STRING(200) NOT NULL,
    DiagnosisDate DATE,
    IsChronic BOOL DEFAULT false,
    Status STRING(20),
    Notes STRING(1000),
    PRIMARY KEY (PatientId, ConditionId)
);

CREATE TABLE patientdb.OutboxMessages (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Type STRING(500) NOT NULL,
    Content JSONB NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    ProcessedAt TIMESTAMPTZ,
    Error STRING(2000),
    RetryCount INT DEFAULT 0,
    INDEX idx_outbox_unprocessed (ProcessedAt) WHERE ProcessedAt IS NULL
);
