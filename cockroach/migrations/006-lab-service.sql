CREATE TABLE his_hope_lab.LabOrders (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    ProviderId UUID NOT NULL,
    EncounterId UUID,
    OrderDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    Status STRING(50) NOT NULL DEFAULT 'Pending',
    Priority STRING(50) NOT NULL DEFAULT 'Routine',
    Notes STRING,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_laborders_status CHECK (Status IN ('Pending', 'Submitted', 'InProgress', 'Completed', 'Cancelled')),
    CONSTRAINT chk_laborders_priority CHECK (Priority IN ('Routine', 'Urgent', 'STAT', 'ASAP')),
    INDEX idx_laborders_patient (PatientId),
    INDEX idx_laborders_provider (ProviderId),
    INDEX idx_laborders_status (Status),
    INDEX idx_laborders_orderdate (OrderDate)
);

CREATE TABLE his_hope_lab.LabTests (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    LabOrderId UUID NOT NULL REFERENCES his_hope_lab.LabOrders(Id) ON DELETE CASCADE,
    TestCode STRING(50) NOT NULL,
    TestName STRING(500) NOT NULL,
    SpecimenType STRING(100),
    Status STRING(50) NOT NULL DEFAULT 'Ordered',
    OrderedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CollectedAt TIMESTAMPTZ,
    CompletedAt TIMESTAMPTZ,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_labtests_status CHECK (Status IN ('Ordered', 'Collected', 'InProgress', 'Resulted', 'Cancelled'))
);

CREATE TABLE his_hope_lab.LabResults (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    LabTestId UUID NOT NULL UNIQUE REFERENCES his_hope_lab.LabTests(Id) ON DELETE CASCADE,
    Value STRING NOT NULL,
    Unit STRING(100),
    ReferenceRange STRING(500),
    AbnormalFlag STRING(50) NOT NULL DEFAULT 'Normal',
    ResultStatus STRING(50) NOT NULL DEFAULT 'Pending',
    ResultedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    PerformedBy UUID,
    Notes STRING,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_labresults_abnormalflag CHECK (AbnormalFlag IN ('Normal', 'Abnormal', 'CriticalHigh', 'CriticalLow')),
    CONSTRAINT chk_labresults_resultstatus CHECK (ResultStatus IN ('Pending', 'Preliminary', 'Final', 'Corrected'))
);

CREATE TABLE his_hope_lab.OutboxMessages (
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
ALTER TABLE his_hope_lab.OutboxMessages ADD COLUMN IF NOT EXISTS CorrelationId STRING(200);
ALTER TABLE his_hope_lab.OutboxMessages ADD COLUMN IF NOT EXISTS CausationId STRING(200);
ALTER TABLE his_hope_lab.OutboxMessages ADD COLUMN IF NOT EXISTS OccurredOn TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE his_hope_lab.OutboxMessages ADD COLUMN IF NOT EXISTS ProcessedOn TIMESTAMPTZ;
ALTER TABLE his_hope_lab.OutboxMessages ADD COLUMN IF NOT EXISTS Status STRING(50) NOT NULL DEFAULT 'Pending';
ALTER TABLE his_hope_lab.OutboxMessages ADD COLUMN IF NOT EXISTS LastRetryOn TIMESTAMPTZ;
ALTER TABLE his_hope_lab.OutboxMessages ADD COLUMN IF NOT EXISTS LockExpiresAt TIMESTAMPTZ;
