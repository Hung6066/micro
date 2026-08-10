-- Pharmacy Service Migration
-- Database: his_hope_pharmacy

CREATE DATABASE IF NOT EXISTS his_hope_pharmacy;

-- "Medications" table matching EF Core MedicationConfiguration
CREATE TABLE "Medications" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Name VARCHAR(200) NOT NULL,
    GenericName VARCHAR(200),
    BrandName VARCHAR(200),
    DosageForm VARCHAR(50) NOT NULL,
    Strength VARCHAR(50) NOT NULL,
    Route VARCHAR(50),
    Category VARCHAR(100),
    Manufacturer VARCHAR(200),
    RequiresPrescription BOOL NOT NULL DEFAULT true,
    IsActive BOOL NOT NULL DEFAULT true,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ
);

-- "Prescriptions" table matching EF Core PrescriptionConfiguration
CREATE TABLE "Prescriptions" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    ProviderId UUID NOT NULL,
    MedicationId UUID,
    MedicationName VARCHAR(200) NOT NULL,
    Strength VARCHAR(50) NOT NULL,
    DosageForm VARCHAR(50) NOT NULL,
    DosageInstructions VARCHAR(500) NOT NULL,
    Route VARCHAR(50),
    Quantity INT NOT NULL,
    Refills INT NOT NULL DEFAULT 0,
    Notes VARCHAR(1000),
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',
    PrescribedDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    ExpiryDate TIMESTAMPTZ,
    FilledDate TIMESTAMPTZ,
    CancelledDate TIMESTAMPTZ,
    CancellationReason VARCHAR(500),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    CONSTRAINT chk_prescriptions_status CHECK (Status IN ('ACTIVE', 'FILLED', 'CANCELLED', 'EXPIRED'))
);

-- "OutboxMessages" table matching shared OutboxMessage entity
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
