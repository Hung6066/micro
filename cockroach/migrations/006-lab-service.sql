CREATE TABLE labdb.LabOrders (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    ProviderId UUID NOT NULL,
    EncounterId UUID,
    OrderDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    Status STRING(50) NOT NULL DEFAULT 'PENDING',
    Priority STRING(50) NOT NULL DEFAULT 'ROUTINE',
    Notes STRING,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_laborders_status CHECK (Status IN ('PENDING', 'SUBMITTED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED')),
    CONSTRAINT chk_laborders_priority CHECK (Priority IN ('ROUTINE', 'URGENT', 'STAT', 'ASAP')),
    INDEX idx_laborders_patient (PatientId),
    INDEX idx_laborders_provider (ProviderId),
    INDEX idx_laborders_status (Status),
    INDEX idx_laborders_orderdate (OrderDate)
);

CREATE TABLE labdb.LabTests (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    LabOrderId UUID NOT NULL REFERENCES labdb.LabOrders(Id) ON DELETE CASCADE,
    TestCode STRING(50) NOT NULL,
    TestName STRING(500) NOT NULL,
    SpecimenType STRING(100),
    Status STRING(50) NOT NULL DEFAULT 'ORDERED',
    OrderedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CollectedAt TIMESTAMPTZ,
    CompletedAt TIMESTAMPTZ,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    CONSTRAINT chk_labtests_status CHECK (Status IN ('ORDERED', 'COLLECTED', 'IN_PROGRESS', 'RESULTED', 'CANCELLED'))
);

CREATE TABLE labdb.LabResults (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    LabTestId UUID NOT NULL UNIQUE REFERENCES labdb.LabTests(Id) ON DELETE CASCADE,
    LabResultId UUID,
    Value STRING NOT NULL,
    Unit STRING(100),
    ReferenceRange STRING(500),
    AbnormalFlag STRING(50) NOT NULL DEFAULT 'NORMAL',
    ResultStatus STRING(50) NOT NULL DEFAULT 'PENDING',
    ResultedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    PerformedBy STRING(200),
    Notes STRING,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_labresults_abnormalflag CHECK (AbnormalFlag IN ('NORMAL', 'ABNORMAL', 'CRITICAL_HIGH', 'CRITICAL_LOW')),
    CONSTRAINT chk_labresults_resultstatus CHECK (ResultStatus IN ('PENDING', 'PRELIMINARY', 'FINAL', 'CORRECTED'))
);

CREATE TABLE labdb.OutboxMessages (
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

-- Backward-compatibility for existing deployments
ALTER TABLE labdb.LabTests ADD COLUMN IF NOT EXISTS UpdatedAt TIMESTAMPTZ;
ALTER TABLE labdb.LabResults ADD COLUMN IF NOT EXISTS LabResultId UUID;
ALTER TABLE labdb.OutboxMessages ADD COLUMN IF NOT EXISTS CorrelationId STRING(200);
ALTER TABLE labdb.OutboxMessages ADD COLUMN IF NOT EXISTS CausationId STRING(200);
ALTER TABLE labdb.OutboxMessages ADD COLUMN IF NOT EXISTS OccurredOn TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE labdb.OutboxMessages ADD COLUMN IF NOT EXISTS ProcessedOn TIMESTAMPTZ;
ALTER TABLE labdb.OutboxMessages ADD COLUMN IF NOT EXISTS Status STRING(50) NOT NULL DEFAULT 'Pending';
ALTER TABLE labdb.OutboxMessages ADD COLUMN IF NOT EXISTS LastRetryOn TIMESTAMPTZ;
ALTER TABLE labdb.OutboxMessages ADD COLUMN IF NOT EXISTS LockExpiresAt TIMESTAMPTZ;

