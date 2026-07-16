CREATE TABLE "LabOrders" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    ProviderId UUID NOT NULL,
    EncounterId UUID,
    OrderDate TIMESTAMPTZ NOT NULL DEFAULT now(),
    Status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    Priority VARCHAR(50) NOT NULL DEFAULT 'Routine',
    Notes TEXT,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_laborders_status CHECK (Status IN ('Pending', 'Submitted', 'InProgress', 'Completed', 'Cancelled')),
    CONSTRAINT chk_laborders_priority CHECK (Priority IN ('Routine', 'Urgent', 'STAT', 'ASAP'))
);

CREATE TABLE "LabTests" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    LabOrderId UUID NOT NULL REFERENCES "LabOrders"(Id) ON DELETE CASCADE,
    TestCode VARCHAR(50) NOT NULL,
    TestName VARCHAR(500) NOT NULL,
    SpecimenType VARCHAR(100),
    Status VARCHAR(50) NOT NULL DEFAULT 'Ordered',
    OrderedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CollectedAt TIMESTAMPTZ,
    CompletedAt TIMESTAMPTZ,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_labtests_status CHECK (Status IN ('Ordered', 'Collected', 'InProgress', 'Resulted', 'Cancelled'))
);

CREATE TABLE "LabResults" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    LabTestId UUID NOT NULL UNIQUE REFERENCES "LabTests"(Id) ON DELETE CASCADE,
    Value TEXT NOT NULL,
    Unit VARCHAR(100),
    ReferenceRange VARCHAR(500),
    AbnormalFlag VARCHAR(50) NOT NULL DEFAULT 'Normal',
    ResultStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    ResultedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    PerformedBy UUID,
    Notes TEXT,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_labresults_abnormalflag CHECK (AbnormalFlag IN ('Normal', 'Abnormal', 'CriticalHigh', 'CriticalLow')),
    CONSTRAINT chk_labresults_resultstatus CHECK (ResultStatus IN ('Pending', 'Preliminary', 'Final', 'Corrected'))
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






