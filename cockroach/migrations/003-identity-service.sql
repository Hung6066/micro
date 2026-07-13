CREATE TABLE identitydb.AspNetUsers (
    Id STRING(36) PRIMARY KEY,
    UserName STRING(256),
    NormalizedUserName STRING(256),
    Email STRING(256),
    NormalizedEmail STRING(256),
    EmailConfirmed BOOL DEFAULT false,
    PasswordHash STRING(500),
    PhoneNumber STRING(20),
    TwoFactorEnabled BOOL DEFAULT false,
    LockoutEnabled BOOL DEFAULT true,
    AccessFailedCount INT DEFAULT 0,
    FullName STRING(200),
    Role STRING(50),
    FacilityId UUID,
    IsActive BOOL DEFAULT true,
    CreatedAt TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX idx_users_normalized_email ON identitydb.AspNetUsers(NormalizedEmail) WHERE NormalizedEmail IS NOT NULL;
CREATE INDEX idx_users_normalized_username ON identitydb.AspNetUsers(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;

CREATE TABLE identitydb.RefreshTokens (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId STRING(36) NOT NULL REFERENCES identitydb.AspNetUsers(Id) ON DELETE CASCADE,
    Token STRING(500) NOT NULL,
    ExpiresAt TIMESTAMPTZ NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    RevokedAt TIMESTAMPTZ,
    INDEX idx_refresh_tokens_user (UserId),
    INDEX idx_refresh_tokens_token (Token)
);

CREATE TABLE identitydb.OutboxMessages (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Type STRING(500) NOT NULL,
    Content JSONB NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    ProcessedAt TIMESTAMPTZ,
    Error STRING(2000),
    RetryCount INT DEFAULT 0,
    INDEX idx_outbox_unprocessed (ProcessedAt) WHERE ProcessedAt IS NULL
);
