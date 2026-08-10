CREATE TABLE "Appointments" (
    AppointmentId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    ProviderId VARCHAR(36) NOT NULL,
    FacilityId UUID,
    ScheduledDate TIMESTAMPTZ NOT NULL,
    DurationMinutes INT NOT NULL DEFAULT 30,
    Status VARCHAR(20) NOT NULL DEFAULT 'Scheduled',
    Reason VARCHAR(500),
    Notes VARCHAR(2000),
    CheckInAt TIMESTAMPTZ,
    CheckOutAt TIMESTAMPTZ,
    CanceledAt TIMESTAMPTZ,
    CancelReason VARCHAR(500),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ
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






