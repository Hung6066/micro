-- ============================================================================
-- His.Hope EMR - Dead Letter Queue Messages Table
-- Version: 015
-- Description: Stores messages that exceeded max retry count in the event bus
-- Idempotent: uses IF NOT EXISTS
-- ============================================================================

CREATE TABLE IF NOT EXISTS dead_letter_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    original_queue VARCHAR(255) NOT NULL,
    exchange VARCHAR(255) NOT NULL,
    routing_key VARCHAR(255) NOT NULL,
    message_body JSONB NOT NULL,
    message_type VARCHAR(500) NOT NULL,
    error_message TEXT,
    retry_count INT NOT NULL DEFAULT 0,
    original_message_id VARCHAR(255),
    occurred_on TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    INDEX idx_dlq_queue (original_queue),
    INDEX idx_dlq_occurred (occurred_on DESC)
);
