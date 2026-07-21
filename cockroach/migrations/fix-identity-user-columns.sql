ALTER TABLE asp_net_users
    ADD COLUMN IF NOT EXISTS failed_login_attempts INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS last_password_changed_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS trusted_device_token VARCHAR(256) NULL;
