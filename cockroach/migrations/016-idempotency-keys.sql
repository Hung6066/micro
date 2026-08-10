-- ============================================================================
-- His.Hope EMR - Idempotency Keys Table
-- Version: 016
-- Description: Stores idempotency keys for safe retries of POST/PUT/PATCH
--              requests. Also tracks processed domain events to ensure
--              at-most-once delivery in the event bus.
-- Idempotent: uses IF NOT EXISTS
-- ============================================================================

CREATE TABLE IF NOT EXISTS idempotency_keys (
    idempotency_key VARCHAR(255) PRIMARY KEY,
    service_name VARCHAR(100) NOT NULL,
    endpoint VARCHAR(500) NOT NULL,
    http_method VARCHAR(10) NOT NULL,
    request_hash VARCHAR(64) NOT NULL,
    response_status_code INT,
    response_body JSONB,
    status VARCHAR(20) NOT NULL DEFAULT 'Processing',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL DEFAULT now() + INTERVAL '24 hours',
    INDEX idx_idempotency_expires (expires_at DESC)
);

CREATE TABLE IF NOT EXISTS processed_events (
    event_id UUID NOT NULL,
    consumer VARCHAR(255) NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (event_id, consumer)
);
