-- Phase 2: User consent records for OAuth2 third-party authorization

CREATE TABLE IF NOT EXISTS openiddict_consents (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL,
    client_id VARCHAR(256) NOT NULL,
    scopes TEXT NOT NULL,
    granted_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ,
    is_active BOOLEAN NOT NULL DEFAULT true,
    revoked_at TIMESTAMPTZ,
    CONSTRAINT pk_openiddict_consents PRIMARY KEY (id),
    CONSTRAINT uq_openiddict_consents_user_client UNIQUE (user_id, client_id)
);

CREATE INDEX IF NOT EXISTS ix_openiddict_consents_user_id ON openiddict_consents (user_id);
CREATE INDEX IF NOT EXISTS ix_openiddict_consents_client_id ON openiddict_consents (client_id);
