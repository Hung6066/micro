-- Pharmacy Service Migration
-- Database: pharmacydb

CREATE DATABASE IF NOT EXISTS pharmacydb;

-- Medications table matching EF Core MedicationConfiguration
CREATE TABLE pharmacydb.Medications (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Name STRING(200) NOT NULL,
    GenericName STRING(200),
    BrandName STRING(200),
    DosageForm STRING(50) NOT NULL,
    Strength STRING(50) NOT NULL,
    Route STRING(50),
    Category STRING(100),
    Manufacturer STRING(200),
    RequiresPrescription BOOL NOT NULL DEFAULT true,
    IsActive BOOL NOT NULL DEFAULT true,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    INDEX idx_medications_name (Name),
    INDEX idx_medications_generic_name (GenericName),
    INDEX idx_medications_active (IsActive)
);

-- Prescriptions table matching EF Core PrescriptionConfiguration
CREATE TABLE pharmacydb.Prescriptions (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    ProviderId UUID NOT NULL,
    MedicationId UUID,
    MedicationName STRING(200) NOT NULL,
    Strength STRING(50) NOT NULL,
    DosageForm STRING(50) NOT NULL,
    DosageInstructions STRING(500) NOT NULL,
    Route STRING(50),
    Quantity INT NOT NULL,
    Refills INT NOT NULL DEFAULT 0,
    Notes STRING(1000),
    Status STRING(20) NOT NULL DEFAULT 'ACTIVE',
    PrescribedDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    ExpiryDate TIMESTAMPTZ,
    FilledDate TIMESTAMPTZ,
    CancelledDate TIMESTAMPTZ,
    CancellationReason STRING(500),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    CONSTRAINT chk_prescriptions_status CHECK (Status IN ('ACTIVE', 'FILLED', 'CANCELLED', 'EXPIRED')),
    INDEX idx_prescriptions_patient (PatientId),
    INDEX idx_prescriptions_provider (ProviderId),
    INDEX idx_prescriptions_status (Status),
    INDEX idx_prescriptions_date (PrescribedDate)
);

-- OutboxMessages table matching shared OutboxMessage entity
CREATE TABLE pharmacydb.OutboxMessages (
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

