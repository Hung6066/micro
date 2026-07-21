CREATE TABLE "LabOrders" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    ProviderId UUID NOT NULL,
    EncounterId UUID,
    OrderDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    Status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
    Priority VARCHAR(50) NOT NULL DEFAULT 'ROUTINE',
    Notes TEXT,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_laborders_status CHECK (Status IN ('PENDING', 'SUBMITTED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED')),
    CONSTRAINT chk_laborders_priority CHECK (Priority IN ('ROUTINE', 'URGENT', 'STAT', 'ASAP'))
);

CREATE TABLE "LabTests" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    LabOrderId UUID NOT NULL REFERENCES "LabOrders"(Id) ON DELETE CASCADE,
    TestCode VARCHAR(50) NOT NULL,
    TestName VARCHAR(500) NOT NULL,
    SpecimenType VARCHAR(100),
    Status VARCHAR(50) NOT NULL DEFAULT 'ORDERED',
    OrderedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CollectedAt TIMESTAMPTZ,
    CompletedAt TIMESTAMPTZ,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    CONSTRAINT chk_labtests_status CHECK (Status IN ('ORDERED', 'COLLECTED', 'IN_PROGRESS', 'RESULTED', 'CANCELLED'))
);

CREATE TABLE "LabResults" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    LabTestId UUID NOT NULL UNIQUE REFERENCES "LabTests"(Id) ON DELETE CASCADE,
    LabResultId UUID,
    Value TEXT NOT NULL,
    Unit VARCHAR(100),
    ReferenceRange VARCHAR(500),
    AbnormalFlag VARCHAR(50) NOT NULL DEFAULT 'NORMAL',
    ResultStatus VARCHAR(50) NOT NULL DEFAULT 'PENDING',
    ResultedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    PerformedBy VARCHAR(200),
    Notes TEXT,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_labresults_abnormalflag CHECK (AbnormalFlag IN ('NORMAL', 'ABNORMAL', 'CRITICAL_HIGH', 'CRITICAL_LOW')),
    CONSTRAINT chk_labresults_resultstatus CHECK (ResultStatus IN ('PENDING', 'PRELIMINARY', 'FINAL', 'CORRECTED'))
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

-- Backward-compatibility for existing deployments
ALTER TABLE "LabTests" ADD COLUMN IF NOT EXISTS UpdatedAt TIMESTAMPTZ;
ALTER TABLE "LabResults" ADD COLUMN IF NOT EXISTS LabResultId UUID;



