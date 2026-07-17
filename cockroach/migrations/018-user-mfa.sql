-- ============================================================================
-- His.Hope EMR - TOTP-based Multi-Factor Authentication
-- Version: 018
-- Description: Creates the UserMfa table, adds MFA columns to AspNetUsers,
--              and seeds MFA-related system settings.
--
-- RFC 6238 compliant: SHA1, 30-second step, 6-digit codes.
-- Recovery codes use SHA256 hashing for storage (never stored in plaintext).
--
-- Idempotent: uses IF NOT EXISTS.
-- Compatible with: CockroachDB 23+
-- ============================================================================

-- ============================================================================
-- SECTION 1: Create UserMfa table
-- ============================================================================
-- Stores per-user MFA configuration.
-- secret_key: base32-encoded TOTP shared secret
-- recovery_codes: SHA256 hashes of recovery codes (never plaintext)
-- backup_codes_used: count of used recovery codes for tracking

CREATE TABLE IF NOT EXISTS identitydb.UserMfa (
    user_id UUID PRIMARY KEY REFERENCES identitydb.AspNetUsers(Id) ON DELETE CASCADE,
    secret_key VARCHAR(100) NOT NULL,
    is_enabled BOOLEAN NOT NULL DEFAULT false,
    enrolled_at TIMESTAMPTZ,
    recovery_codes TEXT[] NOT NULL DEFAULT '{}',
    backup_codes_used INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ============================================================================
-- SECTION 2: Add MFA columns to AspNetUsers
-- ============================================================================
-- mfa_required: when true, user MUST complete MFA on next login
-- mfa_grace_period_ends: if set, user can defer MFA enrollment until this date

ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS mfa_required BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS mfa_grace_period_ends TIMESTAMPTZ;

-- ============================================================================
-- SECTION 3: Add MFA system settings
-- ============================================================================

INSERT INTO identitydb.SystemSettings ("Key", Value, Description, Category, UpdatedAt) VALUES
('mfa.enforceGlobally', 'false', 'Force all users to enroll MFA', 'mfa', now()),
('mfa.gracePeriodDays', '14', 'Days allowed to defer MFA enrollment before enforcement', 'mfa', now()),
('mfa.recoveryCodesPerUser', '8', 'Number of recovery codes generated per user', 'mfa', now())
ON CONFLICT ("Key") DO NOTHING;

-- ============================================================================
-- Migration verification:
--   SELECT * FROM identitydb.UserMfa;
--   SELECT Id, UserName, mfa_required, mfa_grace_period_ends FROM identitydb.AspNetUsers;
-- ============================================================================
