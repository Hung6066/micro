CREATE TABLE appointmentdb.Appointments (
    AppointmentId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PatientId UUID NOT NULL,
    ProviderId STRING(36) NOT NULL,
    FacilityId UUID,
    ScheduledDate TIMESTAMPTZ NOT NULL,
    DurationMinutes INT NOT NULL DEFAULT 30,
    Status STRING(20) NOT NULL DEFAULT 'Scheduled',
    Reason STRING(500),
    Notes STRING(2000),
    CheckInAt TIMESTAMPTZ,
    CheckOutAt TIMESTAMPTZ,
    CanceledAt TIMESTAMPTZ,
    CancelReason STRING(500),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedAt TIMESTAMPTZ,
    INDEX idx_appointments_patient (PatientId),
    INDEX idx_appointments_provider (ProviderId),
    INDEX idx_appointments_date (ScheduledDate),
    INDEX idx_appointments_status (Status) WHERE Status IN ('Scheduled', 'CheckedIn')
);

CREATE TABLE appointmentdb.OutboxMessages (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Type STRING(500) NOT NULL,
    Content JSONB NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    ProcessedAt TIMESTAMPTZ,
    Error STRING(2000),
    RetryCount INT DEFAULT 0,
    INDEX idx_outbox_unprocessed (ProcessedAt) WHERE ProcessedAt IS NULL
);
