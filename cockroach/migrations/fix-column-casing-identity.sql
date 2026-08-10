-- Fix column names to match EF Core UseSnakeCaseNamingConvention
-- Permissions
ALTER TABLE permissions RENAME COLUMN createdat TO created_at;
ALTER TABLE permissions RENAME COLUMN issystem TO is_system;
ALTER TABLE permissions RENAME COLUMN "Group" TO "group";

-- AspNetRoles
ALTER TABLE asp_net_roles RENAME COLUMN createdat TO created_at;
ALTER TABLE asp_net_roles RENAME COLUMN issystem TO is_system;

-- AspNetUserRoles
ALTER TABLE asp_net_user_roles RENAME COLUMN assignedat TO assigned_at;

-- AspNetUsers
ALTER TABLE asp_net_users RENAME COLUMN createdat TO created_at;
ALTER TABLE asp_net_users RENAME COLUMN fullname TO full_name;
ALTER TABLE asp_net_users RENAME COLUMN facilityid TO facility_id;
ALTER TABLE asp_net_users RENAME COLUMN isactive TO is_active;
ALTER TABLE asp_net_users RENAME COLUMN lastloginat TO last_login_at;
ALTER TABLE asp_net_users RENAME COLUMN normalizedusername TO normalized_user_name;
ALTER TABLE asp_net_users RENAME COLUMN normalizedemail TO normalized_email;
ALTER TABLE asp_net_users RENAME COLUMN emailconfirmed TO email_confirmed;
ALTER TABLE asp_net_users RENAME COLUMN passwordhash TO password_hash;
ALTER TABLE asp_net_users RENAME COLUMN phonenumber TO phone_number;
ALTER TABLE asp_net_users RENAME COLUMN twofactorenabled TO two_factor_enabled;
ALTER TABLE asp_net_users RENAME COLUMN lockoutenabled TO lockout_enabled;
ALTER TABLE asp_net_users RENAME COLUMN accessfailedcount TO access_failed_count;
ALTER TABLE asp_net_users RENAME COLUMN phonenumberconfirmed TO phone_number_confirmed;
ALTER TABLE asp_net_users RENAME COLUMN lockoutend TO lockout_end;
ALTER TABLE asp_net_users RENAME COLUMN securitystamp TO security_stamp;
ALTER TABLE asp_net_users RENAME COLUMN concurrencystamp TO concurrency_stamp;
ALTER TABLE asp_net_users RENAME COLUMN licensenumber TO license_number;
ALTER TABLE asp_net_users RENAME COLUMN firstname TO first_name;
ALTER TABLE asp_net_users RENAME COLUMN lastname TO last_name;
ALTER TABLE asp_net_users RENAME COLUMN middlename TO middle_name;

-- Facilities
ALTER TABLE facilities RENAME COLUMN createdat TO created_at;
ALTER TABLE facilities RENAME COLUMN updatedat TO updated_at;
ALTER TABLE facilities RENAME COLUMN isactive TO is_active;
ALTER TABLE facilities RENAME COLUMN nameen TO name_en;
ALTER TABLE facilities RENAME COLUMN facilitytype TO facility_type;

-- AuditLogs
ALTER TABLE audit_logs RENAME COLUMN userid TO user_id;
ALTER TABLE audit_logs RENAME COLUMN username TO user_name;
ALTER TABLE audit_logs RENAME COLUMN resourcetype TO resource_type;
ALTER TABLE audit_logs RENAME COLUMN resourceid TO resource_id;
ALTER TABLE audit_logs RENAME COLUMN ipaddress TO ip_address;
ALTER TABLE audit_logs RENAME COLUMN useragent TO user_agent;

-- RefreshTokens
ALTER TABLE refresh_tokens RENAME COLUMN createdat TO created_at;
ALTER TABLE refresh_tokens RENAME COLUMN expiresat TO expires_at;
ALTER TABLE refresh_tokens RENAME COLUMN revokedat TO revoked_at;

-- RefreshTokenStore
ALTER TABLE refresh_token_store RENAME COLUMN createdat TO created_at;
ALTER TABLE refresh_token_store RENAME COLUMN expiresat TO expires_at;
ALTER TABLE refresh_token_store RENAME COLUMN revokedat TO revoked_at;
ALTER TABLE refresh_token_store RENAME COLUMN tokenhash TO token_hash;
ALTER TABLE refresh_token_store RENAME COLUMN familyid TO family_id;
ALTER TABLE refresh_token_store RENAME COLUMN isrevoked TO is_revoked;
ALTER TABLE refresh_token_store RENAME COLUMN revokedreason TO revoked_reason;
ALTER TABLE refresh_token_store RENAME COLUMN deviceinfo TO device_info;
ALTER TABLE refresh_token_store RENAME COLUMN ipaddress TO ip_address;

-- SystemSettings
ALTER TABLE system_settings RENAME COLUMN updatedat TO updated_at;
ALTER TABLE system_settings RENAME COLUMN updatedby TO updated_by;
