-- Patient aggregate
CREATE TABLE patientdb.Patients (
    PatientId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- PersonName (Value Object)
    FirstName STRING(100) NOT NULL,
    LastName STRING(100) NOT NULL,
    MiddleName STRING(100),
    -- Core fields
    DateOfBirth DATE NOT NULL,
    Gender STRING(10) NOT NULL,
    -- ContactInfo (Value Object)
    Phone STRING(20) NOT NULL,
    Email STRING(200),
    -- Address (Value Object)
    Street STRING(200) NOT NULL,
    District STRING(100),
    City STRING(100) NOT NULL,
    Province STRING(100) NOT NULL,
    PostalCode STRING(20),
    Country STRING(100) NOT NULL,
    -- Enumeration types
    BloodType STRING(10),
    Race STRING(20),
    MaritalStatus STRING(10),
    -- Additional patient fields
    InsuranceId STRING(50),
    NationalId STRING(50),
    Occupation STRING(200),
    EmergencyContactName STRING(200),
    EmergencyContactPhone STRING(20),
    -- Audit fields
    IsActive BOOL NOT NULL DEFAULT true,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    -- Indexes matching EF Core configuration
    INDEX idx_patients_name (LastName, FirstName),
    UNIQUE INDEX idx_patients_active_phone (Phone) WHERE IsActive = true,
    INDEX idx_patients_active (IsActive)
);

CREATE TABLE patientdb.Allergies (
    PatientId UUID NOT NULL REFERENCES patientdb.Patients(PatientId) ON DELETE CASCADE,
    AllergyId UUID NOT NULL DEFAULT gen_random_uuid(),
    Allergen STRING(200) NOT NULL,
    Reaction STRING(500),
    Severity STRING(50),
    RecordedDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    IsActive BOOL NOT NULL DEFAULT true,
    PRIMARY KEY (PatientId, AllergyId)
);

CREATE TABLE patientdb.MedicalConditions (
    PatientId UUID NOT NULL REFERENCES patientdb.Patients(PatientId) ON DELETE CASCADE,
    ConditionId UUID NOT NULL DEFAULT gen_random_uuid(),
    ConditionName STRING(300) NOT NULL,
    Icd10Code STRING(20),
    OnsetDate DATE,
    ResolvedDate TIMESTAMPTZ,
    IsChronic BOOL NOT NULL DEFAULT false,
    Notes STRING(1000),
    RecordedDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    IsActive BOOL NOT NULL DEFAULT true,
    PRIMARY KEY (PatientId, ConditionId),
    INDEX idx_medicalconditions_icd10 (Icd10Code)
);

CREATE TABLE patientdb.OutboxMessages (
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

-- Backward-compatibility ALTER TABLE statements for existing deployments
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS FirstName STRING(100);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS LastName STRING(100);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS MiddleName STRING(100);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS Street STRING(200);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS District STRING(100);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS City STRING(100);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS Province STRING(100);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS PostalCode STRING(20);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS Country STRING(100);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS InsuranceId STRING(50);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS NationalId STRING(50);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS Occupation STRING(200);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS EmergencyContactName STRING(200);
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS EmergencyContactPhone STRING(20);

ALTER TABLE patientdb.Allergies ADD COLUMN IF NOT EXISTS RecordedDate TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE patientdb.Allergies ADD COLUMN IF NOT EXISTS IsActive BOOL NOT NULL DEFAULT true;

ALTER TABLE patientdb.MedicalConditions ADD COLUMN IF NOT EXISTS Icd10Code STRING(20);
ALTER TABLE patientdb.MedicalConditions ADD COLUMN IF NOT EXISTS OnsetDate DATE;
ALTER TABLE patientdb.MedicalConditions ADD COLUMN IF NOT EXISTS ResolvedDate TIMESTAMPTZ;
ALTER TABLE patientdb.MedicalConditions ADD COLUMN IF NOT EXISTS RecordedDate TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE patientdb.MedicalConditions ADD COLUMN IF NOT EXISTS IsActive BOOL NOT NULL DEFAULT true;

ALTER TABLE patientdb.OutboxMessages ADD COLUMN IF NOT EXISTS CorrelationId STRING(200);
ALTER TABLE patientdb.OutboxMessages ADD COLUMN IF NOT EXISTS CausationId STRING(200);
ALTER TABLE patientdb.OutboxMessages ADD COLUMN IF NOT EXISTS OccurredOn TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE patientdb.OutboxMessages ADD COLUMN IF NOT EXISTS ProcessedOn TIMESTAMPTZ;
ALTER TABLE patientdb.OutboxMessages ADD COLUMN IF NOT EXISTS Status STRING(50) NOT NULL DEFAULT 'Pending';
ALTER TABLE patientdb.OutboxMessages ADD COLUMN IF NOT EXISTS LastRetryOn TIMESTAMPTZ;
ALTER TABLE patientdb.OutboxMessages ADD COLUMN IF NOT EXISTS LockExpiresAt TIMESTAMPTZ;
