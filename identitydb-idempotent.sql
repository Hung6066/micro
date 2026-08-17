CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE asp_net_roles (
        id uuid NOT NULL,
        description character varying(500),
        is_system boolean NOT NULL DEFAULT FALSE,
        created_at timestamp with time zone NOT NULL,
        name character varying(256),
        normalized_name character varying(256),
        concurrency_stamp text,
        CONSTRAINT pk_asp_net_roles PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE asp_net_users (
        id uuid NOT NULL,
        first_name character varying(100) NOT NULL,
        last_name character varying(100) NOT NULL,
        middle_name character varying(100),
        license_number character varying(50),
        specialty character varying(200),
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        last_login_at timestamp with time zone,
        failed_login_attempts integer NOT NULL DEFAULT 0,
        lockout_end timestamp with time zone,
        last_password_changed_at timestamp with time zone,
        trusted_device_token character varying(256),
        user_name character varying(256),
        normalized_user_name character varying(256),
        email character varying(256),
        normalized_email character varying(256),
        email_confirmed boolean NOT NULL,
        password_hash text,
        security_stamp text,
        concurrency_stamp text,
        phone_number text,
        phone_number_confirmed boolean NOT NULL,
        two_factor_enabled boolean NOT NULL,
        lockout_enabled boolean NOT NULL,
        access_failed_count integer NOT NULL,
        CONSTRAINT pk_asp_net_users PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE audit_logs (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id character varying(100) NOT NULL,
        user_name character varying(200),
        action character varying(50) NOT NULL,
        resource_type character varying(100) NOT NULL,
        resource_id character varying(100),
        details character varying(2000),
        ip_address character varying(50),
        user_agent character varying(500),
        timestamp timestamp with time zone NOT NULL,
        CONSTRAINT pk_audit_logs PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE openiddict_applications (
        id text NOT NULL,
        application_type text,
        client_id text,
        client_secret text,
        client_type text,
        concurrency_token text,
        consent_type text,
        display_name text,
        display_names text,
        json_web_key_set text,
        permissions text,
        post_logout_redirect_uris text,
        properties text,
        redirect_uris text,
        requirements text,
        settings text,
        CONSTRAINT pk_openiddict_applications PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE openiddict_consents (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        client_id character varying(256) NOT NULL,
        scopes text NOT NULL,
        granted_at timestamp with time zone NOT NULL,
        expires_at timestamp with time zone,
        is_active boolean NOT NULL,
        revoked_at timestamp with time zone,
        CONSTRAINT pk_openiddict_consents PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE openiddict_scopes (
        id text NOT NULL,
        concurrency_token text,
        description text,
        descriptions text,
        display_name text,
        display_names text,
        name text,
        properties text,
        resources text,
        CONSTRAINT pk_openiddict_scopes PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE permissions (
        code character varying(100) NOT NULL,
        name character varying(200) NOT NULL,
        "group" character varying(100) NOT NULL,
        description character varying(500),
        is_system boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_permissions PRIMARY KEY (code)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE security_events (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid,
        user_name character varying(256),
        event_type character varying(50) NOT NULL,
        severity character varying(20) NOT NULL DEFAULT 'info',
        ip_address character varying(50),
        user_agent character varying(500),
        device_info character varying(500),
        details character varying(2000),
        geo_country character varying(100),
        timestamp timestamp with time zone NOT NULL,
        CONSTRAINT pk_security_events PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE system_settings (
        key character varying(200) NOT NULL,
        value character varying(2000) NOT NULL,
        description character varying(500),
        category character varying(100),
        updated_at timestamp with time zone NOT NULL,
        updated_by character varying(100),
        CONSTRAINT pk_system_settings PRIMARY KEY (key)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE asp_net_role_claims (
        id integer GENERATED BY DEFAULT AS IDENTITY,
        role_id uuid NOT NULL,
        claim_type text,
        claim_value text,
        CONSTRAINT pk_asp_net_role_claims PRIMARY KEY (id),
        CONSTRAINT fk_asp_net_role_claims_asp_net_roles_role_id FOREIGN KEY (role_id) REFERENCES asp_net_roles (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE asp_net_user_claims (
        id integer GENERATED BY DEFAULT AS IDENTITY,
        user_id uuid NOT NULL,
        claim_type text,
        claim_value text,
        CONSTRAINT pk_asp_net_user_claims PRIMARY KEY (id),
        CONSTRAINT fk_asp_net_user_claims_asp_net_users_user_id FOREIGN KEY (user_id) REFERENCES asp_net_users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE asp_net_user_logins (
        login_provider text NOT NULL,
        provider_key text NOT NULL,
        provider_display_name text,
        user_id uuid NOT NULL,
        CONSTRAINT pk_asp_net_user_logins PRIMARY KEY (login_provider, provider_key),
        CONSTRAINT fk_asp_net_user_logins_asp_net_users_user_id FOREIGN KEY (user_id) REFERENCES asp_net_users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE asp_net_user_roles (
        user_id uuid NOT NULL,
        role_id uuid NOT NULL,
        CONSTRAINT pk_asp_net_user_roles PRIMARY KEY (user_id, role_id),
        CONSTRAINT fk_asp_net_user_roles_asp_net_roles_role_id FOREIGN KEY (role_id) REFERENCES asp_net_roles (id) ON DELETE CASCADE,
        CONSTRAINT fk_asp_net_user_roles_asp_net_users_user_id FOREIGN KEY (user_id) REFERENCES asp_net_users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE asp_net_user_tokens (
        user_id uuid NOT NULL,
        login_provider text NOT NULL,
        name text NOT NULL,
        value text,
        CONSTRAINT pk_asp_net_user_tokens PRIMARY KEY (user_id, login_provider, name),
        CONSTRAINT fk_asp_net_user_tokens_asp_net_users_user_id FOREIGN KEY (user_id) REFERENCES asp_net_users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE user_mfa (
        user_id uuid NOT NULL,
        secret_key character varying(100) NOT NULL,
        is_enabled boolean NOT NULL DEFAULT FALSE,
        enrolled_at timestamp with time zone,
        recovery_codes text[] NOT NULL,
        backup_codes_used integer NOT NULL DEFAULT 0,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_user_mfa PRIMARY KEY (user_id),
        CONSTRAINT fk_user_mfa_asp_net_users_user_id FOREIGN KEY (user_id) REFERENCES asp_net_users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE openiddict_authorizations (
        id text NOT NULL,
        application_id text,
        concurrency_token text,
        creation_date timestamp with time zone,
        properties text,
        scopes text,
        status text,
        subject text,
        type text,
        CONSTRAINT pk_openiddict_authorizations PRIMARY KEY (id),
        CONSTRAINT fk_openiddict_authorizations_openiddict_applications_applicati FOREIGN KEY (application_id) REFERENCES openiddict_applications (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE role_permissions (
        role_id uuid NOT NULL,
        permission_code character varying(100) NOT NULL,
        CONSTRAINT pk_role_permissions PRIMARY KEY (role_id, permission_code),
        CONSTRAINT fk_role_permissions_permissions_permission_code FOREIGN KEY (permission_code) REFERENCES permissions (code) ON DELETE CASCADE,
        CONSTRAINT fk_role_permissions_roles_role_id FOREIGN KEY (role_id) REFERENCES asp_net_roles (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE TABLE openiddict_tokens (
        id text NOT NULL,
        application_id text,
        authorization_id text,
        concurrency_token text,
        creation_date timestamp with time zone,
        expiration_date timestamp with time zone,
        payload text,
        properties text,
        redemption_date timestamp with time zone,
        reference_id text,
        status text,
        subject text,
        type text,
        CONSTRAINT pk_openiddict_tokens PRIMARY KEY (id),
        CONSTRAINT fk_openiddict_tokens_openiddict_applications_application_id FOREIGN KEY (application_id) REFERENCES openiddict_applications (id),
        CONSTRAINT fk_openiddict_tokens_openiddict_authorizations_authorization_id FOREIGN KEY (authorization_id) REFERENCES openiddict_authorizations (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_asp_net_role_claims_role_id ON asp_net_role_claims (role_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE UNIQUE INDEX "RoleNameIndex" ON asp_net_roles (normalized_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_asp_net_user_claims_user_id ON asp_net_user_claims (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_asp_net_user_logins_user_id ON asp_net_user_logins (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_asp_net_user_roles_role_id ON asp_net_user_roles (role_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX "EmailIndex" ON asp_net_users (normalized_email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE UNIQUE INDEX "UserNameIndex" ON asp_net_users (normalized_user_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_audit_logs_action ON audit_logs (action);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_audit_logs_resource_type ON audit_logs (resource_type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_audit_logs_timestamp ON audit_logs (timestamp);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_audit_logs_user_id ON audit_logs (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_openiddict_authorizations_application_id ON openiddict_authorizations (application_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_openiddict_consents_client_id ON openiddict_consents (client_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_openiddict_consents_user_id ON openiddict_consents (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_openiddict_consents_user_id_client_id ON openiddict_consents (user_id, client_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_openiddict_tokens_application_id ON openiddict_tokens (application_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_openiddict_tokens_authorization_id ON openiddict_tokens (authorization_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_permissions_group ON permissions ("group");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_role_permissions_permission_code ON role_permissions (permission_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_security_events_event_type ON security_events (event_type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_security_events_severity ON security_events (severity);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_security_events_timestamp ON security_events (timestamp);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_security_events_user_id ON security_events (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    CREATE INDEX ix_system_settings_category ON system_settings (category);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260724030124_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260724030124_InitialCreate', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727025755_AddAdminTableViews') THEN
    CREATE TABLE admin_table_views (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        resource character varying(80) NOT NULL,
        name character varying(80) NOT NULL,
        payload_json character varying(65536) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_admin_table_views PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727025755_AddAdminTableViews') THEN
    CREATE UNIQUE INDEX ix_admin_table_views_user_id_resource_name ON admin_table_views (user_id, resource, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727025755_AddAdminTableViews') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260727025755_AddAdminTableViews', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727145241_ExpandEncryptedMfaSecretKey') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260727145241_ExpandEncryptedMfaSecretKey', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727151012_RepairMfaSecretKeyLength') THEN
    ALTER TABLE user_mfa ALTER COLUMN secret_key TYPE character varying(512);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260727151012_RepairMfaSecretKeyLength') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260727151012_RepairMfaSecretKeyLength', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728040334_AddMobilePlatformPersistence') THEN
    CREATE TABLE mobile_device_registrations (
        id uuid NOT NULL,
        user_id character varying(200) NOT NULL,
        platform character varying(20) NOT NULL,
        token_hash character varying(128) NOT NULL,
        token_ciphertext character varying(8192) NOT NULL,
        registered_at timestamp with time zone NOT NULL,
        last_seen_at timestamp with time zone NOT NULL,
        revoked_at timestamp with time zone,
        CONSTRAINT pk_mobile_device_registrations PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728040334_AddMobilePlatformPersistence') THEN
    CREATE TABLE mobile_telemetry_events (
        id uuid NOT NULL,
        event_type character varying(20) NOT NULL,
        name character varying(120) NOT NULL,
        message character varying(2000),
        stack character varying(8000),
        route character varying(500),
        app_version character varying(50) NOT NULL,
        platform character varying(20) NOT NULL,
        duration_ms double precision,
        metadata_json character varying(8000),
        correlation_id character varying(128),
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_mobile_telemetry_events PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728040334_AddMobilePlatformPersistence') THEN
    CREATE UNIQUE INDEX ix_mobile_device_registrations_user_id_platform_token_hash ON mobile_device_registrations (user_id, platform, token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728040334_AddMobilePlatformPersistence') THEN
    CREATE INDEX ix_mobile_device_registrations_user_id_revoked_at ON mobile_device_registrations (user_id, revoked_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728040334_AddMobilePlatformPersistence') THEN
    CREATE INDEX ix_mobile_telemetry_events_event_type_created_at ON mobile_telemetry_events (event_type, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728040334_AddMobilePlatformPersistence') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260728040334_AddMobilePlatformPersistence', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728171600_AddPasskeyCredentials') THEN
    CREATE TABLE passkey_credentials (
        id uuid NOT NULL,
        user_id character varying(200) NOT NULL,
        credential_id character varying(512) NOT NULL,
        public_key character varying(4096) NOT NULL,
        signature_counter bigint NOT NULL,
        created_at timestamp with time zone NOT NULL,
        last_used_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_passkey_credentials PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728171600_AddPasskeyCredentials') THEN
    CREATE UNIQUE INDEX ix_passkey_credentials_user_id_credential_id ON passkey_credentials (user_id, credential_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728171600_AddPasskeyCredentials') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260728171600_AddPasskeyCredentials', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728173929_AddPushNotificationOutbox') THEN
    CREATE TABLE push_notification_outbox (
        id uuid NOT NULL,
        user_id character varying(200) NOT NULL,
        title character varying(200) NOT NULL,
        body character varying(4000) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        available_at timestamp with time zone NOT NULL,
        lease_until timestamp with time zone,
        lease_id uuid,
        attempt_count integer NOT NULL,
        processed_at timestamp with time zone,
        last_error character varying(2000),
        CONSTRAINT pk_push_notification_outbox PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728173929_AddPushNotificationOutbox') THEN
    CREATE INDEX ix_push_notification_outbox_processed_at_available_at ON push_notification_outbox (processed_at, available_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728173929_AddPushNotificationOutbox') THEN
    CREATE INDEX ix_push_notification_outbox_user_id ON push_notification_outbox (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728173929_AddPushNotificationOutbox') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260728173929_AddPushNotificationOutbox', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728180247_EnforceUniquePasskeyCredentialIds') THEN
    CREATE UNIQUE INDEX ix_passkey_credentials_credential_id ON passkey_credentials (credential_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260728180247_EnforceUniquePasskeyCredentialIds') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260728180247_EnforceUniquePasskeyCredentialIds', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260729111128_AddMultilingualLocalization') THEN
    ALTER TABLE asp_net_users ADD preferred_language character varying(35) NOT NULL DEFAULT 'vi-VN';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260729111128_AddMultilingualLocalization') THEN
    CREATE TABLE localization_resources (
        key character varying(200) NOT NULL,
        description character varying(500),
        CONSTRAINT pk_localization_resources PRIMARY KEY (key)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260729111128_AddMultilingualLocalization') THEN
    CREATE TABLE localization_translations (
        resource_key character varying(200) NOT NULL,
        locale character varying(35) NOT NULL,
        value character varying(4000) NOT NULL,
        CONSTRAINT pk_localization_translations PRIMARY KEY (resource_key, locale),
        CONSTRAINT fk_localization_translations_localization_resources_resource_k FOREIGN KEY (resource_key) REFERENCES localization_resources (key) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260729111128_AddMultilingualLocalization') THEN
    CREATE INDEX ix_localization_translations_locale ON localization_translations (locale);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260729111128_AddMultilingualLocalization') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260729111128_AddMultilingualLocalization', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260729112431_SeedMobileAdminLocalization') THEN
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.auth.signIn', 'Mobile admin sign in action');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.auth.signOut', 'Mobile admin sign out action');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.brand.identityAdministration', 'Mobile admin identity service label');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.common.cancel', 'Common cancel action');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.common.delete', 'Common delete action');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.common.empty', 'Common empty state');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.common.error', 'Common error state');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.common.loading', 'Common loading state');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.common.noPermission', 'Common access denied state');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.common.refresh', 'Common refresh action');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.common.retry', 'Common retry action');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.common.save', 'Common save action');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.common.search', 'Common search action');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.mfa.alreadyEnabled', 'MFA enabled status');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.mfa.createPasskey', 'Passkey registration action');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.mfa.passkeyRegistrationFailed', 'Passkey registration error');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.mfa.setupFailed', 'MFA setup error');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.mfa.title', 'MFA security screen title');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.mfa.verify', 'MFA verification action');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.nav.clients', 'Mobile admin navigation: clients');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.nav.consents', 'Mobile admin navigation: consents');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.nav.home', 'Mobile admin navigation: home');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.nav.roles', 'Mobile admin navigation: roles');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.nav.settings', 'Mobile admin navigation: settings');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.nav.users', 'Mobile admin navigation: users');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.providers.ldap', 'LDAP directory provider');
    INSERT INTO localization_resources (key, description)
    VALUES ('mobile.providers.saml', 'SAML identity provider');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260729112431_SeedMobileAdminLocalization') THEN
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.auth.signIn', 'Sign in');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.auth.signIn', 'Đăng nhập');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.auth.signOut', 'Sign out');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.auth.signOut', 'Đăng xuất');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.brand.identityAdministration', 'IDENTITY ADMINISTRATION');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.brand.identityAdministration', 'QUẢN TRỊ DANH TÍNH');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.common.cancel', 'Cancel');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.common.cancel', 'Hủy');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.common.delete', 'Delete');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.common.delete', 'Xóa');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.common.empty', 'No data available');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.common.empty', 'Chưa có dữ liệu');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.common.error', 'Something went wrong');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.common.error', 'Đã xảy ra lỗi');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.common.loading', 'Loading...');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.common.loading', 'Đang tải...');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.common.noPermission', 'Access denied');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.common.noPermission', 'Không có quyền truy cập');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.common.refresh', 'Refresh');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.common.refresh', 'Làm mới');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.common.retry', 'Retry');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.common.retry', 'Thử lại');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.common.save', 'Save');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.common.save', 'Lưu');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.common.search', 'Search');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.common.search', 'Tìm kiếm');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.mfa.alreadyEnabled', 'MFA is already enabled');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.mfa.alreadyEnabled', 'MFA đã được bật');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.mfa.createPasskey', 'Create passkey');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.mfa.createPasskey', 'Tạo passkey');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.mfa.passkeyRegistrationFailed', 'Passkey registration failed');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.mfa.passkeyRegistrationFailed', 'Đăng ký passkey thất bại');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.mfa.setupFailed', 'Unable to start MFA setup');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.mfa.setupFailed', 'Không thể bắt đầu thiết lập MFA');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.mfa.title', 'MFA security');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.mfa.title', 'Bảo mật MFA');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.mfa.verify', 'Verify');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.mfa.verify', 'Xác minh');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.nav.clients', 'Clients');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.nav.clients', 'Ứng dụng');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.nav.consents', 'Consents');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.nav.consents', 'Chấp thuận');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.nav.home', 'Home');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.nav.home', 'Trang chủ');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.nav.roles', 'Roles');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.nav.roles', 'Vai trò');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.nav.settings', 'Settings');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.nav.settings', 'Cài đặt');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.nav.users', 'Users');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.nav.users', 'Người dùng');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.providers.ldap', 'LDAP/AD');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.providers.ldap', 'LDAP/AD');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('en-US', 'mobile.providers.saml', 'SAML SSO');
    INSERT INTO localization_translations (locale, resource_key, value)
    VALUES ('vi-VN', 'mobile.providers.saml', 'Đăng nhập SSO SAML');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260729112431_SeedMobileAdminLocalization') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260729112431_SeedMobileAdminLocalization', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730032830_AddUserFacilityMembership') THEN
    CREATE TABLE user_facilities (
        user_id uuid NOT NULL,
        facility_id character varying(100) NOT NULL,
        is_primary boolean NOT NULL,
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        revoked_at timestamp with time zone,
        CONSTRAINT pk_user_facilities PRIMARY KEY (user_id, facility_id),
        CONSTRAINT fk_user_facilities_users_user_id FOREIGN KEY (user_id) REFERENCES asp_net_users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730032830_AddUserFacilityMembership') THEN
    CREATE INDEX ix_user_facilities_facility_id_is_active ON user_facilities (facility_id, is_active);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730032830_AddUserFacilityMembership') THEN
    CREATE INDEX ix_user_facilities_user_id_is_primary ON user_facilities (user_id, is_primary);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260730032830_AddUserFacilityMembership') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260730032830_AddUserFacilityMembership', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260803012030_AddInAppNotifications') THEN
    CREATE TABLE in_app_notifications (
        id uuid NOT NULL,
        user_id character varying(200) NOT NULL,
        title character varying(200) NOT NULL,
        body character varying(4000) NOT NULL,
        data_json character varying(8000),
        created_at timestamp with time zone NOT NULL,
        read_at timestamp with time zone,
        CONSTRAINT pk_in_app_notifications PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260803012030_AddInAppNotifications') THEN
    CREATE INDEX ix_in_app_notifications_user_id_read_at_created_at ON in_app_notifications (user_id, read_at, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260803012030_AddInAppNotifications') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260803012030_AddInAppNotifications', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260803041702_AddMobileDeliveryAttempts') THEN
    CREATE TABLE push_delivery_attempts (
        id uuid NOT NULL,
        outbox_id uuid NOT NULL,
        device_id uuid NOT NULL,
        platform character varying(20) NOT NULL,
        status character varying(30) NOT NULL,
        error_code character varying(200),
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_push_delivery_attempts PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260803041702_AddMobileDeliveryAttempts') THEN
    CREATE INDEX ix_push_delivery_attempts_created_at_platform_status ON push_delivery_attempts (created_at, platform, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260803041702_AddMobileDeliveryAttempts') THEN
    CREATE INDEX ix_push_delivery_attempts_device_id ON push_delivery_attempts (device_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260803041702_AddMobileDeliveryAttempts') THEN
    CREATE INDEX ix_push_delivery_attempts_outbox_id ON push_delivery_attempts (outbox_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260803041702_AddMobileDeliveryAttempts') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260803041702_AddMobileDeliveryAttempts', '8.0.10');
    END IF;
END $EF$;
COMMIT;

