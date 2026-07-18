-- Drop FK constraints referencing tables we need to alter
ALTER TABLE role_permissions DROP CONSTRAINT IF EXISTS "RolePermissions_roleid_fkey";
ALTER TABLE role_permissions DROP CONSTRAINT IF EXISTS "RolePermissions_permissioncode_fkey";
ALTER TABLE refresh_tokens DROP CONSTRAINT IF EXISTS "RefreshTokens_userid_fkey";
ALTER TABLE refresh_token_store DROP CONSTRAINT IF EXISTS "RefreshTokenStore_userid_fkey";
ALTER TABLE refresh_token_store DROP CONSTRAINT IF EXISTS "refreshtokenstore_userid_fkey";
ALTER TABLE asp_net_user_roles DROP CONSTRAINT IF EXISTS "AspNetUserRoles_userid_fkey";
ALTER TABLE asp_net_user_roles DROP CONSTRAINT IF EXISTS "AspNetUserRoles_roleid_fkey";

-- Drop PK constraints on tables being altered
ALTER TABLE asp_net_roles DROP CONSTRAINT IF EXISTS "AspNetRoles_pkey";
ALTER TABLE asp_net_users DROP CONSTRAINT IF EXISTS "AspNetUsers_pkey";

-- Convert VARCHAR(36) ID columns to UUID
ALTER TABLE role_permissions ALTER COLUMN role_id TYPE UUID USING role_id::uuid;
ALTER TABLE asp_net_user_roles ALTER COLUMN userid TYPE UUID USING userid::uuid;
ALTER TABLE asp_net_user_roles ALTER COLUMN roleid TYPE UUID USING roleid::uuid;
ALTER TABLE refresh_tokens ALTER COLUMN userid TYPE UUID USING userid::uuid;
ALTER TABLE refresh_token_store ALTER COLUMN userid TYPE UUID USING userid::uuid;
ALTER TABLE asp_net_users ALTER COLUMN id TYPE UUID USING id::uuid;
ALTER TABLE asp_net_roles ALTER COLUMN id TYPE UUID USING id::uuid;

-- Re-add PK constraints
ALTER TABLE asp_net_roles ADD PRIMARY KEY (id);
ALTER TABLE asp_net_users ADD PRIMARY KEY (id);

-- Re-add FK constraints
ALTER TABLE refresh_tokens ADD FOREIGN KEY (userid) REFERENCES asp_net_users(id) ON DELETE CASCADE;
ALTER TABLE asp_net_user_roles ADD FOREIGN KEY (userid) REFERENCES asp_net_users(id) ON DELETE CASCADE;
ALTER TABLE asp_net_user_roles ADD FOREIGN KEY (roleid) REFERENCES asp_net_roles(id) ON DELETE CASCADE;
ALTER TABLE role_permissions ADD FOREIGN KEY (role_id) REFERENCES asp_net_roles(id) ON DELETE CASCADE;
ALTER TABLE role_permissions ADD FOREIGN KEY (permission_code) REFERENCES permissions(code) ON DELETE CASCADE;
ALTER TABLE refresh_token_store ADD FOREIGN KEY (userid) REFERENCES asp_net_users(id) ON DELETE CASCADE;
