-- ============================================================================
-- His.Hope EMR - Identity Extensions: Permissions, Roles, RBAC Seed Data
-- Version: 013
-- Description: Creates the Permissions, AspNetRoles, AspNetUserRoles, and
--              RolePermissions tables with full seed data. Also creates the
--              enhanced RefreshTokenStore table and adds missing ASP.NET
--              Identity columns to AspNetUsers.
--
--              This is THE critical RBAC migration — all authorization checks
--              depend on the data seeded here.
--
-- Idempotent: uses IF NOT EXISTS and ON CONFLICT DO NOTHING.
-- Compatible with: CockroachDB 23+
-- ============================================================================

-- ============================================================================
-- SECTION 1: Add missing ASP.NET Identity columns to AspNetUsers
-- ============================================================================
-- The existing 003 migration created a minimal AspNetUsers table. These ALTER
-- statements add columns needed by the User entity and Identity framework.

-- Custom entity columns
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS FirstName STRING(100);
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS LastName STRING(100);
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS MiddleName STRING(100);
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS LicenseNumber STRING(50);
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS Specialty STRING(200);
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS LastLoginAt TIMESTAMPTZ;

-- ASP.NET Identity base columns (missing from 003)
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS SecurityStamp STRING(256);
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS ConcurrencyStamp STRING(256) DEFAULT gen_random_uuid()::STRING;
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS PhoneNumberConfirmed BOOL DEFAULT false;
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS LockoutEnd TIMESTAMPTZ;
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS TwoFactorEnabled BOOL DEFAULT false;

-- Add CreatedBy column for RLS tracking (who created the patient record)
ALTER TABLE identitydb.AspNetUsers ADD COLUMN IF NOT EXISTS FacilityId UUID;

-- ============================================================================
-- SECTION 2: Create Permissions table (idempotent)
-- ============================================================================
-- The Permission entity uses Code as the primary key (string), matching the
-- EF Core configuration: entity.HasKey(p => p.Code)

CREATE TABLE IF NOT EXISTS identitydb.Permissions (
    Code STRING(100) PRIMARY KEY,
    Name STRING(200) NOT NULL,
    "Group" STRING(100) NOT NULL,
    Description STRING(500),
    IsSystem BOOL NOT NULL DEFAULT true,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_permissions_group ON identitydb.Permissions("Group");

-- ============================================================================
-- SECTION 3: Create AspNetRoles table (idempotent)
-- ============================================================================
-- The Role entity extends IdentityRole<Guid>. The existing pg-010 migration
-- defined this for PostgreSQL; this is the CockroachDB equivalent.

CREATE TABLE IF NOT EXISTS identitydb.AspNetRoles (
    Id STRING(36) PRIMARY KEY,
    Name STRING(256) NOT NULL,
    NormalizedName STRING(256) NOT NULL,
    ConcurrencyStamp STRING(256),
    Description STRING(500),
    IsSystem BOOL NOT NULL DEFAULT false,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_aspnetroles_name ON identitydb.AspNetRoles(NormalizedName);

-- ============================================================================
-- SECTION 4: Create AspNetUserRoles join table (idempotent)
-- ============================================================================
-- The UserRole entity extends IdentityUserRole<Guid> and adds AssignedAt.
-- EF Core maps this to AspNetUserRoles by convention.

CREATE TABLE IF NOT EXISTS identitydb.AspNetUserRoles (
    UserId STRING(36) NOT NULL,
    RoleId STRING(36) NOT NULL,
    AssignedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (UserId, RoleId),
    FOREIGN KEY (UserId) REFERENCES identitydb.AspNetUsers(Id) ON DELETE CASCADE,
    FOREIGN KEY (RoleId) REFERENCES identitydb.AspNetRoles(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_aspnetuserroles_roleid ON identitydb.AspNetUserRoles(RoleId);

-- ============================================================================
-- SECTION 5: Create RolePermissions join table (idempotent)
-- ============================================================================
-- Many-to-many: Role <-> Permission. Composite PK matches EF Core config:
-- entity.HasKey(rp => new { rp.RoleId, rp.PermissionCode });

CREATE TABLE IF NOT EXISTS identitydb.RolePermissions (
    RoleId STRING(36) NOT NULL,
    PermissionCode STRING(100) NOT NULL,
    PRIMARY KEY (RoleId, PermissionCode),
    FOREIGN KEY (RoleId) REFERENCES identitydb.AspNetRoles(Id) ON DELETE CASCADE,
    FOREIGN KEY (PermissionCode) REFERENCES identitydb.Permissions(Code) ON DELETE CASCADE
);

-- ============================================================================
-- SECTION 6: Create enhanced RefreshTokenStore table
-- ============================================================================
-- Replaces the simple RefreshTokens table from 003 with a production-grade
-- design supporting token family rotation, device fingerprinting, and
-- revocation tracking. The old table is kept for backward compatibility;
-- new code uses this table via the RefreshTokenStore EF Core entity.

CREATE TABLE IF NOT EXISTS identitydb.RefreshTokenStore (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId STRING(36) NOT NULL REFERENCES identitydb.AspNetUsers(Id) ON DELETE CASCADE,
    TokenHash STRING(128) NOT NULL,
    FamilyId STRING(64) NOT NULL,
    IsRevoked BOOL DEFAULT false,
    RevokedReason STRING(200),
    DeviceInfo STRING(500),
    IpAddress STRING(45),
    CreatedAt TIMESTAMPTZ DEFAULT now(),
    ExpiresAt TIMESTAMPTZ NOT NULL,
    RevokedAt TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_refreshtokenstore_hash ON identitydb.RefreshTokenStore(TokenHash);
CREATE INDEX IF NOT EXISTS idx_refreshtokenstore_userid ON identitydb.RefreshTokenStore(UserId);
CREATE INDEX IF NOT EXISTS idx_refreshtokenstore_family ON identitydb.RefreshTokenStore(FamilyId);

-- ============================================================================
-- SECTION 7: Create SystemSettings table (idempotent)
-- ============================================================================

CREATE TABLE IF NOT EXISTS identitydb.SystemSettings (
    "Key" STRING(200) PRIMARY KEY,
    Value STRING(2000) NOT NULL,
    Description STRING(500),
    Category STRING(100),
    UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    UpdatedBy STRING(100)
);

CREATE INDEX IF NOT EXISTS idx_systemsettings_category ON identitydb.SystemSettings(Category);

-- ============================================================================
-- SECTION 8: Add FacilityId and CreatedBy to Patients table (for RLS)
-- ============================================================================

ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS FacilityId UUID;
ALTER TABLE patientdb.Patients ADD COLUMN IF NOT EXISTS CreatedBy STRING(36);

-- ============================================================================
-- SECTION 9: Add FacilityId to Encounters table (for RLS)
-- ============================================================================

ALTER TABLE clinicaldb.Encounters ADD COLUMN IF NOT EXISTS FacilityId UUID;

-- ============================================================================
-- ============================================================================
-- SEED DATA BEGINS HERE
-- ============================================================================
-- ============================================================================

-- ============================================================================
-- SECTION 10: Seed Permissions (comprehensive list)
-- ============================================================================
-- These are the canonical permissions used across all services. The
-- PermissionGuard attribute in the API layer references these codes.

INSERT INTO identitydb.Permissions (Code, Name, "Group", Description, IsSystem) VALUES
-- Patient module
('patients.view', 'Xem bệnh nhân', 'Bệnh nhân', 'Xem danh sách và thông tin bệnh nhân', true),
('patients.create', 'Thêm bệnh nhân', 'Bệnh nhân', 'Tạo hồ sơ bệnh nhân mới', true),
('patients.update', 'Cập nhật bệnh nhân', 'Bệnh nhân', 'Chỉnh sửa thông tin bệnh nhân', true),
('patients.delete', 'Xóa bệnh nhân', 'Bệnh nhân', 'Xóa hồ sơ bệnh nhân', true),
('patients.export', 'Xuất dữ liệu bệnh nhân', 'Bệnh nhân', 'Xuất dữ liệu bệnh nhân ra file', true),
('patients.manage', 'Quản lý bệnh nhân', 'Bệnh nhân', 'Quản lý toàn bộ module bệnh nhân', true),

-- Appointment module
('appointments.view', 'Xem lịch hẹn', 'Lịch hẹn', 'Xem danh sách lịch hẹn', true),
('appointments.create', 'Tạo lịch hẹn', 'Lịch hẹn', 'Tạo lịch hẹn mới', true),
('appointments.update', 'Cập nhật lịch hẹn', 'Lịch hẹn', 'Chỉnh sửa lịch hẹn', true),
('appointments.cancel', 'Hủy lịch hẹn', 'Lịch hẹn', 'Hủy lịch hẹn đã đặt', true),
('appointments.check-in', 'Check-in bệnh nhân', 'Lịch hẹn', 'Check-in bệnh nhân đến khám', true),
('appointments.manage', 'Quản lý lịch hẹn', 'Lịch hẹn', 'Quản lý toàn bộ module lịch hẹn', true),

-- Clinical / Encounter module
('clinical.view', 'Xem khám bệnh', 'Khám bệnh', 'Xem thông tin khám bệnh', true),
('clinical.create', 'Tạo khám bệnh', 'Khám bệnh', 'Tạo phiếu khám bệnh mới', true),
('clinical.update', 'Cập nhật khám bệnh', 'Khám bệnh', 'Chỉnh sửa thông tin khám bệnh', true),
('clinical.sign', 'Ký khám bệnh', 'Khám bệnh', 'Ký xác nhận hoàn thành khám bệnh', true),
('clinical.delete', 'Xóa khám bệnh', 'Khám bệnh', 'Xóa phiếu khám bệnh', true),
('clinical.manage', 'Quản lý khám bệnh', 'Khám bệnh', 'Quản lý toàn bộ module khám bệnh', true),

-- Lab module
('lab.view', 'Xem xét nghiệm', 'Xét nghiệm', 'Xem danh sách và kết quả xét nghiệm', true),
('lab.create', 'Tạo yêu cầu xét nghiệm', 'Xét nghiệm', 'Tạo yêu cầu xét nghiệm mới', true),
('lab.update', 'Cập nhật xét nghiệm', 'Xét nghiệm', 'Cập nhật thông tin xét nghiệm', true),
('lab.result', 'Nhập kết quả xét nghiệm', 'Xét nghiệm', 'Nhập và xác nhận kết quả xét nghiệm', true),
('lab.approve', 'Phê duyệt kết quả', 'Xét nghiệm', 'Phê duyệt kết quả xét nghiệm', true),
('lab.cancel', 'Hủy xét nghiệm', 'Xét nghiệm', 'Hủy yêu cầu xét nghiệm', true),
('lab.manage', 'Quản lý xét nghiệm', 'Xét nghiệm', 'Quản lý toàn bộ module xét nghiệm', true),

-- Billing module
('billing.view', 'Xem hóa đơn', 'Thanh toán', 'Xem danh sách hóa đơn', true),
('billing.create', 'Tạo hóa đơn', 'Thanh toán', 'Tạo hóa đơn mới', true),
('billing.update', 'Cập nhật hóa đơn', 'Thanh toán', 'Chỉnh sửa hóa đơn', true),
('billing.void', 'Hủy hóa đơn', 'Thanh toán', 'Hủy/Void hóa đơn', true),
('billing.pay', 'Thanh toán hóa đơn', 'Thanh toán', 'Xử lý thanh toán hóa đơn', true),
('billing.manage', 'Quản lý thanh toán', 'Thanh toán', 'Quản lý toàn bộ module thanh toán', true),

-- Pharmacy module
('pharmacy.view', 'Xem thuốc', 'Dược', 'Xem danh mục và thông tin thuốc', true),
('pharmacy.create', 'Kê đơn thuốc', 'Dược', 'Tạo đơn thuốc mới', true),
('pharmacy.update', 'Cập nhật đơn thuốc', 'Dược', 'Chỉnh sửa đơn thuốc', true),
('pharmacy.dispense', 'Xuất thuốc', 'Dược', 'Xuất/phát thuốc cho bệnh nhân', true),
('pharmacy.cancel', 'Hủy đơn thuốc', 'Dược', 'Hủy đơn thuốc', true),
('pharmacy.manage', 'Quản lý dược', 'Dược', 'Quản lý toàn bộ module dược', true),

-- Admin / Identity module
('admin.users.read', 'Xem người dùng', 'Quản trị', 'Xem danh sách người dùng', true),
('admin.users.write', 'Quản lý người dùng', 'Quản trị', 'Tạo/sửa/xóa người dùng', true),
('admin.roles.read', 'Xem vai trò', 'Quản trị', 'Xem danh sách vai trò', true),
('admin.roles.write', 'Quản lý vai trò', 'Quản trị', 'Tạo/sửa/xóa vai trò', true),
('admin.permissions.read', 'Xem quyền', 'Quản trị', 'Xem danh sách quyền', true),
('admin.permissions.write', 'Quản lý quyền', 'Quản trị', 'Gán/gỡ quyền cho vai trò', true),
('admin.settings.read', 'Xem cài đặt', 'Quản trị', 'Xem cài đặt hệ thống', true),
('admin.settings.write', 'Quản lý cài đặt', 'Quản trị', 'Thay đổi cài đặt hệ thống', true),
('admin.audit.read', 'Xem nhật ký', 'Quản trị', 'Xem nhật ký hệ thống', true),

-- Reports module
('reports.view', 'Xem báo cáo', 'Báo cáo', 'Xem báo cáo thống kê', true),
('reports.export', 'Xuất báo cáo', 'Báo cáo', 'Xuất báo cáo ra file', true),
('reports.manage', 'Quản lý báo cáo', 'Báo cáo', 'Quản lý mẫu báo cáo', true)
ON CONFLICT (Code) DO NOTHING;

-- ============================================================================
-- SECTION 11: Seed Roles (AspNetRoles)
-- ============================================================================

INSERT INTO identitydb.AspNetRoles (Id, Name, NormalizedName, Description, IsSystem, CreatedAt, ConcurrencyStamp) VALUES
('00000000-0000-0000-0000-000000000001', 'Admin', 'ADMIN', 'Quản trị viên hệ thống — toàn quyền trên tất cả modules', true, now(), gen_random_uuid()::STRING),
('00000000-0000-0000-0000-000000000002', 'Provider', 'PROVIDER', 'Bác sĩ — khám bệnh, kê đơn, xem kết quả xét nghiệm', true, now(), gen_random_uuid()::STRING),
('00000000-0000-0000-0000-000000000003', 'Nurse', 'NURSE', 'Điều dưỡng — hỗ trợ khám, theo dõi bệnh nhân', true, now(), gen_random_uuid()::STRING),
('00000000-0000-0000-0000-000000000004', 'Receptionist', 'RECEPTIONIST', 'Lễ tân — tiếp nhận, đặt lịch hẹn', true, now(), gen_random_uuid()::STRING),
('00000000-0000-0000-0000-000000000005', 'LabTechnician', 'LABTECHNICIAN', 'Kỹ thuật viên xét nghiệm — thực hiện và nhập kết quả', true, now(), gen_random_uuid()::STRING),
('00000000-0000-0000-0000-000000000006', 'Pharmacist', 'PHARMACIST', 'Dược sĩ — quản lý thuốc, cấp phát thuốc', true, now(), gen_random_uuid()::STRING),
('00000000-0000-0000-0000-000000000007', 'BillingClerk', 'BILLINGCLERK', 'Nhân viên thanh toán — hóa đơn, thu ngân', true, now(), gen_random_uuid()::STRING)
ON CONFLICT (Id) DO NOTHING;

-- ============================================================================
-- SECTION 12: Seed Role-Permission Mappings
-- ============================================================================

-- 12a. Admin — gets ALL permissions
INSERT INTO identitydb.RolePermissions (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000001', Code FROM identitydb.Permissions p
WHERE NOT EXISTS (
    SELECT 1 FROM identitydb.RolePermissions rp
    WHERE rp.RoleId = '00000000-0000-0000-0000-000000000001' AND rp.PermissionCode = p.Code
);

-- 12b. Provider — clinical + patient read + lab read/create + pharmacy
INSERT INTO identitydb.RolePermissions (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000002', p.Code FROM identitydb.Permissions p
WHERE p.Code IN (
    'patients.view', 'patients.create', 'patients.update', 'patients.export',
    'appointments.view', 'appointments.create', 'appointments.update', 'appointments.cancel',
    'clinical.view', 'clinical.create', 'clinical.update', 'clinical.sign',
    'lab.view', 'lab.create', 'lab.result',
    'pharmacy.view', 'pharmacy.create', 'pharmacy.dispense',
    'billing.view',
    'reports.view', 'reports.export'
)
AND NOT EXISTS (
    SELECT 1 FROM identitydb.RolePermissions rp
    WHERE rp.RoleId = '00000000-0000-0000-0000-000000000002' AND rp.PermissionCode = p.Code
);

-- 12c. Nurse — patient read + appointment checkin + clinical read/create
INSERT INTO identitydb.RolePermissions (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000003', p.Code FROM identitydb.Permissions p
WHERE p.Code IN (
    'patients.view', 'patients.update',
    'appointments.view', 'appointments.check-in', 'appointments.update',
    'clinical.view', 'clinical.create', 'clinical.update',
    'lab.view', 'lab.create',
    'pharmacy.view'
)
AND NOT EXISTS (
    SELECT 1 FROM identitydb.RolePermissions rp
    WHERE rp.RoleId = '00000000-0000-0000-0000-000000000003' AND rp.PermissionCode = p.Code
);

-- 12d. Receptionist — patient read/create + appointment management + billing view
INSERT INTO identitydb.RolePermissions (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000004', p.Code FROM identitydb.Permissions p
WHERE p.Code IN (
    'patients.view', 'patients.create',
    'appointments.view', 'appointments.create', 'appointments.check-in',
    'billing.view', 'billing.create', 'billing.update'
)
AND NOT EXISTS (
    SELECT 1 FROM identitydb.RolePermissions rp
    WHERE rp.RoleId = '00000000-0000-0000-0000-000000000004' AND rp.PermissionCode = p.Code
);

-- 12e. LabTechnician — patient read + lab full access
INSERT INTO identitydb.RolePermissions (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000005', p.Code FROM identitydb.Permissions p
WHERE p.Code IN (
    'patients.view',
    'lab.view', 'lab.create', 'lab.update', 'lab.result', 'lab.approve'
)
AND NOT EXISTS (
    SELECT 1 FROM identitydb.RolePermissions rp
    WHERE rp.RoleId = '00000000-0000-0000-0000-000000000005' AND rp.PermissionCode = p.Code
);

-- 12f. Pharmacist — patient read + pharmacy full access
INSERT INTO identitydb.RolePermissions (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000006', p.Code FROM identitydb.Permissions p
WHERE p.Code IN (
    'patients.view',
    'pharmacy.view', 'pharmacy.update', 'pharmacy.dispense'
)
AND NOT EXISTS (
    SELECT 1 FROM identitydb.RolePermissions rp
    WHERE rp.RoleId = '00000000-0000-0000-0000-000000000006' AND rp.PermissionCode = p.Code
);

-- 12g. BillingClerk — patient read + billing full access
INSERT INTO identitydb.RolePermissions (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000007', p.Code FROM identitydb.Permissions p
WHERE p.Code IN (
    'patients.view',
    'billing.view', 'billing.create', 'billing.update', 'billing.void', 'billing.pay'
)
AND NOT EXISTS (
    SELECT 1 FROM identitydb.RolePermissions rp
    WHERE rp.RoleId = '00000000-0000-0000-0000-000000000007' AND rp.PermissionCode = p.Code
);

-- ============================================================================
-- SECTION 13: Seed System Settings
-- ============================================================================

INSERT INTO identitydb.SystemSettings ("Key", Value, Description, Category, UpdatedAt) VALUES
('hospital.name', 'Bệnh viện His.Hope', 'Tên bệnh viện hiển thị trên toàn hệ thống', 'hospital', now()),
('hospital.address', '123 Đường Lê Lợi, Quận 1, TP.HCM', 'Địa chỉ bệnh viện', 'hospital', now()),
('hospital.phone', '028.1234.5678', 'Số điện thoại liên hệ bệnh viện', 'hospital', now()),
('hospital.email', 'contact@hishop.vn', 'Email liên hệ bệnh viện', 'hospital', now()),
('system.defaultLanguage', 'vi', 'Ngôn ngữ mặc định của hệ thống (vi, en)', 'system', now()),
('system.sessionTimeout', '480', 'Thời gian timeout phiên làm việc (phút)', 'system', now()),
('system.enableMfa', 'false', 'Bật/tắt xác thực đa yếu tố', 'system', now()),
('system.enableRefreshTokenRotation', 'true', 'Bật/tắt refresh token rotation (family-based)', 'system', now()),
('clinical.autoSaveInterval', '300', 'Khoảng thời gian tự động lưu hồ sơ (giây)', 'clinical', now()),
('billing.defaultCurrency', 'VND', 'Đơn vị tiền tệ mặc định', 'billing', now()),
('billing.taxRate', '8', 'Thuế suất mặc định (%)', 'billing', now())
ON CONFLICT ("Key") DO NOTHING;

-- ============================================================================
-- SECTION 14: Migration verification queries
-- ============================================================================
-- Run these queries to verify the migration was applied successfully:
--
--   -- Count permissions
--   SELECT COUNT(*) AS permission_count FROM identitydb.Permissions;
--
--   -- Count roles
--   SELECT Name, COUNT(rp.PermissionCode) AS permission_count
--   FROM identitydb.AspNetRoles r
--   LEFT JOIN identitydb.RolePermissions rp ON r.Id = rp.RoleId
--   GROUP BY r.Name, r.Id
--   ORDER BY r.Name;
--
--   -- Check a specific user's effective permissions
--   SELECT p.Code, p.Name, p."Group"
--   FROM identitydb.AspNetUserRoles ur
--   JOIN identitydb.RolePermissions rp ON ur.RoleId = rp.RoleId
--   JOIN identitydb.Permissions p ON rp.PermissionCode = p.Code
--   WHERE ur.UserId = '00000000-0000-0000-0000-000000000101'
--   ORDER BY p."Group", p.Code;
-- ============================================================================
