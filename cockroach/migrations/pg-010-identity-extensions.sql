-- ============================================================================
-- His.Hope EMR - Identity Service Extensions
-- Version: pg-010
-- Description: Roles, "Permissions", UserRoles, "SystemSettings", "AuditLogs"
-- Target DB: identitydb
-- Idempotent: uses IF NOT EXISTS for all creates.
-- Compatible with: PostgreSQL 16+, CockroachDB
-- ============================================================================
-- Usage: psql -U postgres -d identitydb -f pg-010-identity-extensions.sql
-- ============================================================================

BEGIN;

-- ============================================================================
-- SECTION 1: "Permissions"
-- ============================================================================
CREATE TABLE IF NOT EXISTS "Permissions" (
    Code VARCHAR(100) PRIMARY KEY,
    Name VARCHAR(200) NOT NULL,
    "Group" VARCHAR(100) NOT NULL,
    Description VARCHAR(500),
    IsSystem BOOL NOT NULL DEFAULT true,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_permissions_group ON "Permissions"("Group");

-- ============================================================================
-- SECTION 2: "AspNetRoles" + "AspNetUserRoles" (must exist before FK references)
-- ============================================================================
CREATE TABLE IF NOT EXISTS "AspNetRoles" (
    Id VARCHAR(36) PRIMARY KEY,
    Name VARCHAR(256) NOT NULL,
    NormalizedName VARCHAR(256) NOT NULL,
    ConcurrencyStamp VARCHAR(256),
    IsSystem BOOL NOT NULL DEFAULT false,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    Description VARCHAR(500)
);

CREATE TABLE IF NOT EXISTS "AspNetUserRoles" (
    UserId VARCHAR(36) NOT NULL REFERENCES "AspNetUsers"(Id) ON DELETE CASCADE,
    RoleId VARCHAR(36) NOT NULL REFERENCES "AspNetRoles"(Id) ON DELETE CASCADE,
    AssignedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (UserId, RoleId)
);

-- ============================================================================
-- SECTION 3: "RolePermissions" (many-to-many: Role <-> Permission)
-- ============================================================================
CREATE TABLE IF NOT EXISTS "RolePermissions" (
    RoleId VARCHAR(36) NOT NULL,
    PermissionCode VARCHAR(100) NOT NULL,
    PRIMARY KEY (RoleId, PermissionCode),
    FOREIGN KEY (RoleId) REFERENCES "AspNetRoles"(Id) ON DELETE CASCADE,
    FOREIGN KEY (PermissionCode) REFERENCES "Permissions"(Code) ON DELETE CASCADE
);

-- ============================================================================
-- SECTION 4: "SystemSettings"
-- ============================================================================
CREATE TABLE IF NOT EXISTS "SystemSettings" (
    "Key" VARCHAR(200) PRIMARY KEY,
    Value VARCHAR(2000) NOT NULL,
    Description VARCHAR(500),
    Category VARCHAR(100),
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedBy VARCHAR(100)
);

CREATE INDEX IF NOT EXISTS idx_systemsettings_category ON "SystemSettings"(Category);

-- ============================================================================
-- SECTION 6: "AuditLogs" (HIPAA compliance)
-- ============================================================================
CREATE TABLE IF NOT EXISTS "AuditLogs" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId VARCHAR(100) NOT NULL,
    UserName VARCHAR(200),
    Action VARCHAR(50) NOT NULL,
    ResourceType VARCHAR(100) NOT NULL,
    ResourceId VARCHAR(100),
    Details VARCHAR(2000),
    IpAddress VARCHAR(50),
    UserAgent VARCHAR(500),
    Timestamp TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_auditlogs_userid ON "AuditLogs"(UserId);
CREATE INDEX IF NOT EXISTS idx_auditlogs_resource ON "AuditLogs"(ResourceType, ResourceId);
CREATE INDEX IF NOT EXISTS idx_auditlogs_action ON "AuditLogs"(Action);
CREATE INDEX IF NOT EXISTS idx_auditlogs_timestamp ON "AuditLogs"(Timestamp);

-- ============================================================================
-- SECTION 7: Seed default "Permissions"
-- ============================================================================
INSERT INTO "Permissions" (Code, Name, "Group", Description, IsSystem) VALUES
-- "Patients"
('"Patients".read', 'Xem bệnh nhân', 'Bệnh nhân', 'Xem thông tin bệnh nhân', true),
('"Patients".write', 'Cập nhật bệnh nhân', 'Bệnh nhân', 'Cập nhật thông tin bệnh nhân', true),
('"Patients".delete', 'Xóa bệnh nhân', 'Bệnh nhân', 'Xóa bệnh nhân khỏi hệ thống', true),
-- "Appointments"
('"Appointments".read', 'Xem lịch hẹn', 'Lịch hẹn', 'Xem lịch hẹn khám bệnh', true),
('"Appointments".write', 'Quản lý lịch hẹn', 'Lịch hẹn', 'Tạo và cập nhật lịch hẹn', true),
-- Clinical
('clinical.read', 'Xem hồ sơ bệnh án', 'Lâm sàng', 'Xem hồ sơ bệnh án, toa thuốc', true),
('clinical.write', 'Ghi chép lâm sàng', 'Lâm sàng', 'Ghi chép kết quả khám, SOAP, toa thuốc', true),
-- Lab
('lab.read', 'Xem xét nghiệm', 'Xét nghiệm', 'Xem kết quả xét nghiệm', true),
('lab.write', 'Chỉ định xét nghiệm', 'Xét nghiệm', 'Tạo yêu cầu xét nghiệm', true),
('lab.result', 'Nhập kết quả xét nghiệm', 'Xét nghiệm', 'Nhập và xác nhận kết quả xét nghiệm', true),
-- Pharmacy
('pharmacy.read', 'Xem kho thuốc', 'Dược', 'Xem thông tin thuốc và tồn kho', true),
('pharmacy.write', 'Quản lý thuốc', 'Dược', 'Cập nhật thông tin thuốc', true),
('pharmacy.dispense', 'Cấp phát thuốc', 'Dược', 'Cấp phát thuốc theo toa', true),
-- Billing
('billing.read', 'Xem hóa đơn', 'Tài chính', 'Xem hóa đơn và thanh toán', true),
('billing.write', 'Quản lý hóa đơn', 'Tài chính', 'Tạo và cập nhật hóa đơn', true),
-- Users
('users.read', 'Xem người dùng', 'Người dùng', 'Xem danh sách người dùng', true),
('users.write', 'Quản lý người dùng', 'Người dùng', 'Tạo và cập nhật người dùng', true),
('users.manage_roles', 'Quản lý vai trò', 'Người dùng', 'Gán vai trò và phân quyền', true),
-- Settings
('settings.read', 'Xem cấu hình', 'Cấu hình', 'Xem cấu hình hệ thống', true),
('settings.write', 'Cập nhật cấu hình', 'Cấu hình', 'Cập nhật cấu hình hệ thống', true),
-- Audit
('audit.read', 'Xem nhật ký', 'Kiểm toán', 'Xem nhật ký truy cập PHI', true)
ON CONFLICT (Code) DO NOTHING;

-- ============================================================================
-- SECTION 8: Seed admin role with all "Permissions"
-- ============================================================================
DO $$
DECLARE
    admin_role_id UUID;
BEGIN
    -- Get the Admin role ID (create if not exists)
    INSERT INTO "AspNetRoles" (Id, Name, NormalizedName, Description, IsSystem, CreatedAt)
    VALUES (
        '00000000-0000-0000-0000-000000000001',
        'Admin',
        'ADMIN',
        'Quản trị viên hệ thống - có toàn quyền',
        true,
        now()
    )
    ON CONFLICT (Id) DO UPDATE SET
        Name = EXCLUDED.Name,
        NormalizedName = EXCLUDED.NormalizedName,
        Description = EXCLUDED.Description
    RETURNING Id INTO admin_role_id;

    -- Assign all "Permissions" to Admin role
    INSERT INTO "RolePermissions" (RoleId, PermissionCode)
    SELECT admin_role_id, Code FROM "Permissions"
    ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

    -- Create other system roles
    INSERT INTO "AspNetRoles" (Id, Name, NormalizedName, Description, IsSystem, CreatedAt) VALUES
    ('00000000-0000-0000-0000-000000000002', 'Provider', 'PROVIDER', 'Bác sĩ - khám và điều trị', true, now()),
    ('00000000-0000-0000-0000-000000000003', 'Nurse', 'NURSE', 'Điều dưỡng - hỗ trợ khám bệnh', true, now()),
    ('00000000-0000-0000-0000-000000000004', 'Receptionist', 'RECEPTIONIST', 'Lễ tân - tiếp nhận bệnh nhân', true, now()),
    ('00000000-0000-0000-0000-000000000005', 'LabTechnician', 'LABTECHNICIAN', 'Kỹ thuật viên xét nghiệm', true, now()),
    ('00000000-0000-0000-0000-000000000006', 'Pharmacist', 'PHARMACIST', 'Dược sĩ - cấp phát thuốc', true, now())
    ON CONFLICT (Id) DO NOTHING;

    -- Assign role-specific "Permissions"
    -- Provider: "Patients", "Appointments", clinical, lab.read, pharmacy.read, billing.read
    INSERT INTO "RolePermissions" (RoleId, PermissionCode)
    SELECT '00000000-0000-0000-0000-000000000002', Code FROM "Permissions"
    WHERE Code IN (
        '"Patients".read', '"Patients".write',
        '"Appointments".read', '"Appointments".write',
        'clinical.read', 'clinical.write',
        'lab.read', 'lab.write',
        'pharmacy.read',
        'billing.read'
    )
    ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

    -- Nurse: "Patients".read, "Appointments".read, clinical.read, clinical.write
    INSERT INTO "RolePermissions" (RoleId, PermissionCode)
    SELECT '00000000-0000-0000-0000-000000000003', Code FROM "Permissions"
    WHERE Code IN (
        '"Patients".read',
        '"Appointments".read',
        'clinical.read', 'clinical.write'
    )
    ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

    -- Receptionist: "Patients".read, "Patients".write, "Appointments".read, "Appointments".write
    INSERT INTO "RolePermissions" (RoleId, PermissionCode)
    SELECT '00000000-0000-0000-0000-000000000004', Code FROM "Permissions"
    WHERE Code IN (
        '"Patients".read', '"Patients".write',
        '"Appointments".read', '"Appointments".write'
    )
    ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

    -- LabTechnician: lab.read, lab.write, lab.result
    INSERT INTO "RolePermissions" (RoleId, PermissionCode)
    SELECT '00000000-0000-0000-0000-000000000005', Code FROM "Permissions"
    WHERE Code IN (
        'lab.read', 'lab.write', 'lab.result',
        '"Patients".read'
    )
    ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

    -- Pharmacist: pharmacy.read, pharmacy.write, pharmacy.dispense
    INSERT INTO "RolePermissions" (RoleId, PermissionCode)
    SELECT '00000000-0000-0000-0000-000000000006', Code FROM "Permissions"
    WHERE Code IN (
        'pharmacy.read', 'pharmacy.write', 'pharmacy.dispense',
        '"Patients".read'
    )
    ON CONFLICT (RoleId, PermissionCode) DO NOTHING;
END $$;

-- ============================================================================
-- SECTION 9: Seed default system settings
-- ============================================================================
INSERT INTO "SystemSettings" ("Key", Value, Description, Category, UpdatedAt) VALUES
('hospital.name', 'Bệnh viện His.Hope', 'Tên bệnh viện hiển thị trên toàn hệ thống', 'hospital', now()),
('hospital.address', '123 Đường Lê Lợi, Quận 1, TP.HCM', 'Địa chỉ bệnh viện', 'hospital', now()),
('hospital.phone', '028.1234.5678', 'Số điện thoại liên hệ bệnh viện', 'hospital', now()),
('hospital.email', 'contact@hishop.vn', 'Email liên hệ bệnh viện', 'hospital', now()),
('system.defaultLanguage', 'vi', 'Ngôn ngữ mặc định của hệ thống (vi, en)', 'system', now()),
('system.sessionTimeout', '480', 'Thời gian timeout phiên làm việc (phút)', 'system', now()),
('system.enableMfa', 'false', 'Bật/tắt xác thực đa yếu tố', 'system', now()),
('clinical.autoSaveInterval', '300', 'Khoảng thời gian tự động lưu hồ sơ (giây)', 'clinical', now()),
('billing.defaultCurrency', 'VND', 'Đơn vị tiền tệ mặc định', 'billing', now()),
('billing.taxRate', '8', 'Thuế suất mặc định (%)', 'billing', now())
ON CONFLICT ("Key") DO NOTHING;

COMMIT;
