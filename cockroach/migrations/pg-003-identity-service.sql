CREATE TABLE "AspNetUsers" (
    Id VARCHAR(36) PRIMARY KEY,
    UserName VARCHAR(256),
    NormalizedUserName VARCHAR(256),
    Email VARCHAR(256),
    NormalizedEmail VARCHAR(256),
    EmailConfirmed BOOL DEFAULT false,
    PasswordHash VARCHAR(500),
    PhoneNumber VARCHAR(20),
    TwoFactorEnabled BOOL DEFAULT false,
    LockoutEnabled BOOL DEFAULT true,
    AccessFailedCount INT DEFAULT 0,
    FullName VARCHAR(200),
    Role VARCHAR(50),
    FacilityId UUID,
    IsActive BOOL DEFAULT true,
    CreatedAt TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX idx_users_normalized_email ON "AspNetUsers"(NormalizedEmail) WHERE NormalizedEmail IS NOT NULL;
CREATE INDEX idx_users_normalized_username ON "AspNetUsers"(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;

CREATE TABLE "RefreshTokens" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId VARCHAR(36) NOT NULL REFERENCES "AspNetUsers"(Id) ON DELETE CASCADE,
    Token VARCHAR(500) NOT NULL,
    ExpiresAt TIMESTAMPTZ NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    RevokedAt TIMESTAMPTZ
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






