-- ============================================================================
-- His.Hope EMR - Saga Instances Table
-- Version: 021
-- Description: Stores persistent saga state for distributed transaction
--              orchestration. Each row represents one saga execution with its
--              current status, step index, serialized data, and error info.
--              Supports timeout recovery via heartbeat monitoring.
--
-- The PersistentSagaOrchestrator writes to this table on each step transition.
-- The SagaRecoveryService scans for stale heartbeats and resumes or compensates.
--
-- Idempotent: uses IF NOT EXISTS.
-- Compatible with: CockroachDB 23+
-- ============================================================================

-- ============================================================================
-- SECTION 1: Create saga_instances table
-- ============================================================================

CREATE TABLE IF NOT EXISTS saga_instances (
    saga_id UUID PRIMARY KEY,
    saga_type VARCHAR(255) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    step_index INT NOT NULL DEFAULT 0,
    data JSONB NOT NULL,
    error_message TEXT,
    started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at TIMESTAMPTZ,
    last_heartbeat TIMESTAMPTZ NOT NULL DEFAULT now(),

    INDEX idx_saga_status (status, started_at),
    INDEX idx_saga_heartbeat (last_heartbeat) WHERE status IN ('Running', 'Compensating')
);

-- ============================================================================
-- SECTION 2: Add table comment (CockroachDB syntax)
-- ============================================================================

COMMENT ON TABLE saga_instances IS
    'Persistent state for distributed saga orchestration with timeout recovery';

COMMENT ON COLUMN saga_instances.saga_id IS
    'Unique identifier for this saga execution';
COMMENT ON COLUMN saga_instances.saga_type IS
    'Dotnet type name of the saga orchestrator (e.g., PatientRegistrationSaga)';
COMMENT ON COLUMN saga_instances.status IS
    'Current status: Pending, Running, Completed, Failed, Compensating, Compensated';
COMMENT ON COLUMN saga_instances.step_index IS
    'Index of the last successfully completed step (0-based)';
COMMENT ON COLUMN saga_instances.data IS
    'Serialized saga data payload (JSONB)';
COMMENT ON COLUMN saga_instances.error_message IS
    'Error message if the saga failed or is being compensated';
COMMENT ON COLUMN saga_instances.started_at IS
    'When the saga execution started';
COMMENT ON COLUMN saga_instances.completed_at IS
    'When the saga completed or was fully compensated';
COMMENT ON COLUMN saga_instances.last_heartbeat IS
    'Last heartbeat timestamp; used by SagaRecoveryService to detect stale sagas';

-- ============================================================================
-- Migration verification:
--   SELECT count(*) FROM saga_instances;
--   SELECT * FROM saga_instances LIMIT 10;
-- ============================================================================
