-- Phase 1 OIDC: OpenIddict tables for OAuth2/OIDC authorization server

CREATE TABLE IF NOT EXISTS openiddict_applications (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    application_id VARCHAR(256),
    client_id VARCHAR(256) NOT NULL,
    client_secret VARCHAR(512),
    concurrency_token VARCHAR(256),
    consent_type VARCHAR(50),
    display_name VARCHAR(256),
    display_names TEXT,
    permissions TEXT,
    post_logout_redirect_uris TEXT,
    properties TEXT,
    redirect_uris TEXT,
    requirements TEXT,
    type VARCHAR(50) NOT NULL DEFAULT 'public',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ,
    CONSTRAINT pk_openiddict_applications PRIMARY KEY (id),
    CONSTRAINT uq_openiddict_applications_client_id UNIQUE (client_id)
);

CREATE TABLE IF NOT EXISTS openiddict_authorizations (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    application_id UUID REFERENCES openiddict_applications(id),
    concurrency_token VARCHAR(256),
    creation_date TIMESTAMPTZ,
    properties TEXT,
    scopes TEXT,
    status VARCHAR(50),
    subject VARCHAR(256),
    type VARCHAR(50),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ,
    CONSTRAINT pk_openiddict_authorizations PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_openiddict_authorizations_application_id
    ON openiddict_authorizations (application_id);
CREATE INDEX IF NOT EXISTS ix_openiddict_authorizations_subject
    ON openiddict_authorizations (subject);

CREATE TABLE IF NOT EXISTS openiddict_scopes (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    concurrency_token VARCHAR(256),
    description VARCHAR(500),
    descriptions TEXT,
    display_name VARCHAR(256),
    display_names TEXT,
    name VARCHAR(256) NOT NULL,
    properties TEXT,
    resources TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ,
    CONSTRAINT pk_openiddict_scopes PRIMARY KEY (id),
    CONSTRAINT uq_openiddict_scopes_name UNIQUE (name)
);

CREATE TABLE IF NOT EXISTS openiddict_tokens (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    application_id UUID REFERENCES openiddict_applications(id),
    authorization_id UUID REFERENCES openiddict_authorizations(id),
    concurrency_token VARCHAR(256),
    creation_date TIMESTAMPTZ,
    expiration_date TIMESTAMPTZ,
    payload TEXT,
    properties TEXT,
    redemption_date TIMESTAMPTZ,
    reference_id VARCHAR(256),
    status VARCHAR(50),
    subject VARCHAR(256),
    type VARCHAR(50),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ,
    CONSTRAINT pk_openiddict_tokens PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_openiddict_tokens_reference_id
    ON openiddict_tokens (reference_id);
CREATE INDEX IF NOT EXISTS ix_openiddict_tokens_application_id
    ON openiddict_tokens (application_id);
CREATE INDEX IF NOT EXISTS ix_openiddict_tokens_authorization_id
    ON openiddict_tokens (authorization_id);
CREATE INDEX IF NOT EXISTS ix_openiddict_tokens_subject
    ON openiddict_tokens (subject);
CREATE INDEX IF NOT EXISTS ix_openiddict_tokens_status
    ON openiddict_tokens (status);
