-- Patient aggregate
CREATE TABLE "Patients" (
    PatientId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- PersonName (Value Object)
    FirstName VARCHAR(100) NOT NULL,
    LastName VARCHAR(100) NOT NULL,
    MiddleName VARCHAR(100),
    -- Core fields
    DateOfBirth DATE NOT NULL,
    Gender VARCHAR(10) NOT NULL,
    -- ContactInfo (Value Object)
    Phone VARCHAR(20) NOT NULL,
    Email VARCHAR(200),
    -- Address (Value Object)
    Street VARCHAR(200) NOT NULL,
    District VARCHAR(100),
    City VARCHAR(100) NOT NULL,
    Province VARCHAR(100) NOT NULL,
    PostalCode VARCHAR(20),
    Country VARCHAR(100) NOT NULL,
    -- Enumeration types
    BloodType VARCHAR(10),
    Race VARCHAR(20),
    MaritalStatus VARCHAR(10),
    -- Additional patient fields
    InsuranceId VARCHAR(50),
    NationalId VARCHAR(50),
    Occupation VARCHAR(200),
    EmergencyContactName VARCHAR(200),
    EmergencyContactPhone VARCHAR(20),
    -- Audit fields
    IsActive BOOL NOT NULL DEFAULT true,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ
);

CREATE TABLE "Allergies" (
    PatientId UUID NOT NULL REFERENCES "Patients"(PatientId) ON DELETE CASCADE,
    AllergyId UUID NOT NULL DEFAULT gen_random_uuid(),
    Allergen VARCHAR(200) NOT NULL,
    Reaction VARCHAR(500),
    Severity VARCHAR(50),
    RecordedDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    IsActive BOOL NOT NULL DEFAULT true,
    PRIMARY KEY (PatientId, AllergyId)
);

CREATE TABLE "MedicalConditions" (
    PatientId UUID NOT NULL REFERENCES "Patients"(PatientId) ON DELETE CASCADE,
    ConditionId UUID NOT NULL DEFAULT gen_random_uuid(),
    ConditionName VARCHAR(300) NOT NULL,
    Icd10Code VARCHAR(20),
    OnsetDate DATE,
    ResolvedDate TIMESTAMPTZ,
    IsChronic BOOL NOT NULL DEFAULT false,
    Notes VARCHAR(1000),
    RecordedDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    IsActive BOOL NOT NULL DEFAULT true,
    PRIMARY KEY (PatientId, ConditionId)
);

CREATE TABLE "OutboxMessages" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Type VARCHAR(500) NOT NULL,
    Content JSONB NOT NULL,
    CorrelationId VARCHAR(200),
    CausationId VARCHAR(200),
    OccurredOn TIMESTAMPTZ NOT NULL DEFAULT now(),
    ProcessedOn TIMESTAMPTZ,
    Status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    Error VARCHAR(1000),
    RetryCount INT DEFAULT 0,
    LastRetryOn TIMESTAMPTZ,
    LockExpiresAt TIMESTAMPTZ
);

-- Backward-compatibility ALTER TABLE statements for existing deployments






























