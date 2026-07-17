-- ============================================================================
-- His.Hope EMR - Login Attempts Audit Table for Brute Force Protection
-- Version: 020
-- Description: Creates login_attempts table to track successful and failed
--              login attempts per user and IP address. Used for audit trail
--              and brute force detection analysis.
--
-- The BruteForceProtectionService uses Redis for fast counters but writes
-- every attempt to this table for a permanent audit trail (HIPAA compliance).
--
-- Idempotent: uses IF NOT EXISTS.
-- Compatible with: CockroachDB 23+
-- ============================================================================

-- ============================================================================
-- SECTION 1: Create login_attempts table
-- ============================================================================
-- Stores one row per login attempt (both success and failure).
-- Retention: Managed by data lifecycle archival jobs (see soft-delete pattern).
-- This table is append-only; no updates, no deletes.

CREATE TABLE IF NOT EXISTS identitydb.login_attempts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES identitydb.AspNetUsers(Id) ON DELETE CASCADE,
    ip_address VARCHAR(45) NOT NULL,
    attempted_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_successful BOOL NOT NULL
);

-- Index for querying attempts by user (e.g., "how many failures for user X?")
CREATE INDEX IF NOT EXISTS idx_login_attempts_user_time
    ON identitydb.login_attempts (user_id, attempted_at);

-- Index for querying attempts by IP (e.g., "is this IP brute-forcing?")
CREATE INDEX IF NOT EXISTS idx_login_attempts_ip_time
    ON identitydb.login_attempts (ip_address, attempted_at);

-- ============================================================================
-- SECTION 2: Add table comment (CockroachDB syntax)
-- ============================================================================

COMMENT ON TABLE identitydb.login_attempts IS
    'Audit log of all login attempts for brute force detection and HIPAA compliance';

COMMENT ON COLUMN identitydb.login_attempts.user_id IS
    'FK to AspNetUsers; NULL for attempts where username does not exist';

COMMENT ON COLUMN identitydb.login_attempts.ip_address IS
    'Client IP address (supports IPv4 and IPv6)';

COMMENT ON COLUMN identitydb.login_attempts.is_successful IS
    'true = successful authentication, false = failed attempt';

-- ============================================================================
-- Migration verification:
--   SELECT count(*) FROM identitydb.login_attempts;
--   SELECT * FROM identitydb.login_attempts LIMIT 10;
-- ============================================================================
