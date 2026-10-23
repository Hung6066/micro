-- Production hardening for the shared saga state contract.
-- Safe to run during rolling deployment; all additions are additive.

ALTER TABLE saga_instances ADD COLUMN IF NOT EXISTS tenant_key VARCHAR(200);
ALTER TABLE saga_instances ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(200);
ALTER TABLE saga_instances ADD COLUMN IF NOT EXISTS causation_id VARCHAR(200);
ALTER TABLE saga_instances ADD COLUMN IF NOT EXISTS idempotency_key VARCHAR(500);
ALTER TABLE saga_instances ADD COLUMN IF NOT EXISTS retry_count INT NOT NULL DEFAULT 0;
ALTER TABLE saga_instances ADD COLUMN IF NOT EXISTS version INT8 NOT NULL DEFAULT 0;
ALTER TABLE saga_instances ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT now();

CREATE UNIQUE INDEX IF NOT EXISTS ux_saga_idempotency
    ON saga_instances (saga_type, idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_saga_tenant_status
    ON saga_instances (tenant_key, status, updated_at);
