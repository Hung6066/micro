-- ============================================================================
-- His.Hope EMR - Operation Status Table
-- Version: 023
-- Description: Stores the state of long-running operations for the
--              async request-reply pattern (Prefer: respond-async).
--              Supports polling via GET /api/v1/operations/{id}.
-- Idempotent: uses IF NOT EXISTS
-- ============================================================================

CREATE TABLE IF NOT EXISTS operation_status (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    operation_type VARCHAR(100) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Queued',
    progress INT NOT NULL DEFAULT 0,
    request_data JSONB,
    result_data JSONB,
    error_message TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ NOT NULL DEFAULT now() + INTERVAL '24 hours',
    INDEX idx_operation_status_expires (expires_at DESC),
    INDEX idx_operation_status_status (status)
);
