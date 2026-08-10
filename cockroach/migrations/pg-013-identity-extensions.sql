-- ============================================================================
-- His.Hope EMR - Identity Extensions: Enriched RBAC, Facilities, RLS Columns
-- Version: pg-013
-- Description: Adds enhanced identity columns, RefreshTokenStore, facility
--              management tables, and comprehensive RBAC seed data including
--              the BillingClerk role. Also adds FacilityId/CreatedBy columns
--              to Patient/Encounter tables for row-level security.
--
-- Idempotent: uses IF NOT EXISTS / ON CONFLICT DO NOTHING.
-- Compatible with: PostgreSQL 16+
-- ============================================================================
-- Usage: psql -U postgres -d identitydb -f pg-013-identity-extensions.sql
--        (also connects to patientdb and clinicaldb for ALTER TABLEs)
-- ============================================================================

-- ============================================================================
-- SECTION 1: Add missing ASP.NET Identity columns to AspNetUsers
-- ============================================================================

\c identitydb

-- Enable pgcrypto for gen_random_bytes() used in refresh token seed data
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Extended user profile columns
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "FirstName" VARCHAR(100);
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "LastName" VARCHAR(100);
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "MiddleName" VARCHAR(100);
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "LicenseNumber" VARCHAR(50);
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "Specialty" VARCHAR(200);
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "LastLoginAt" TIMESTAMPTZ;

-- ASP.NET Identity base columns that may be missing
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "SecurityStamp" VARCHAR(256);
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "ConcurrencyStamp" VARCHAR(256) DEFAULT gen_random_uuid()::VARCHAR;
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "PhoneNumberConfirmed" BOOL DEFAULT false;
ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "LockoutEnd" TIMESTAMPTZ;

-- ============================================================================
-- SECTION 2: Create RefreshTokenStore table (token family rotation)
-- ============================================================================

CREATE TABLE IF NOT EXISTS "RefreshTokenStore" (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    userid VARCHAR(36) NOT NULL,
    tokenhash VARCHAR(128) NOT NULL,
    familyid VARCHAR(64) NOT NULL,
    isrevoked BOOL DEFAULT false,
    revokedreason VARCHAR(200),
    deviceinfo VARCHAR(500),
    ipaddress VARCHAR(45),
    createdat TIMESTAMPTZ DEFAULT now(),
    expiresat TIMESTAMPTZ NOT NULL,
    revokedat TIMESTAMPTZ
);

-- Add FK constraint if not exists (check first to avoid error)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'refreshtokenstore_userid_fkey'
    ) THEN
        ALTER TABLE "RefreshTokenStore" ADD CONSTRAINT refreshtokenstore_userid_fkey
            FOREIGN KEY (userid) REFERENCES "AspNetUsers"(id) ON DELETE CASCADE;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS idx_refreshtokenstore_hash ON "RefreshTokenStore"(tokenhash);
CREATE INDEX IF NOT EXISTS idx_refreshtokenstore_userid ON "RefreshTokenStore"(userid);
CREATE INDEX IF NOT EXISTS idx_refreshtokenstore_family ON "RefreshTokenStore"(familyid);

-- ============================================================================
-- SECTION 3: Create Facilities table (multi-facility support)
-- ============================================================================

CREATE TABLE IF NOT EXISTS "Facilities" (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Code VARCHAR(20) NOT NULL UNIQUE,
    Name VARCHAR(200) NOT NULL,
    NameEn VARCHAR(200),
    Address VARCHAR(500),
    Phone VARCHAR(20),
    Email VARCHAR(200),
    Website VARCHAR(200),
    FacilityType VARCHAR(50) NOT NULL DEFAULT 'Hospital',
    IsActive BOOL DEFAULT true,
    Timezone VARCHAR(50) DEFAULT 'Asia/Ho_Chi_Minh',
    CreatedAt TIMESTAMPTZ DEFAULT now(),
    UpdatedAt TIMESTAMPTZ
);

-- ============================================================================
-- SECTION 4: Add FacilityId / CreatedBy to cross-service tables (for RLS)
-- ============================================================================

\c patientdb

ALTER TABLE "Patients" ADD COLUMN IF NOT EXISTS "FacilityId" UUID;
ALTER TABLE "Patients" ADD COLUMN IF NOT EXISTS "CreatedBy" VARCHAR(36);

\c clinicaldb

ALTER TABLE "Encounters" ADD COLUMN IF NOT EXISTS "FacilityId" UUID;

\c identitydb

-- ============================================================================
-- SECTION 5: SEED DATA — Facilities
-- ============================================================================

INSERT INTO "Facilities" (id, code, name, nameen, address, phone, facilitytype, isactive, timezone) VALUES
('11111111-1111-1111-1111-111111111111', 'HCM-HQ', 'Bệnh viện His.Hope Cơ Sở Chính',
 'His.Hope Main Hospital', '123 Đường Lê Lợi, Quận 1, TP. Hồ Chí Minh',
 '028.1234.5678', 'Hospital', true, 'Asia/Ho_Chi_Minh'),
('22222222-2222-2222-2222-222222222222', 'HN-BR', 'Phòng khám His.Hope Bà Triệu Hà Nội',
 'His.Hope Ba Trieu Clinic Hanoi', '456 Phố Bà Triệu, Quận Hai Bà Trưng, Hà Nội',
 '024.5678.1234', 'Clinic', true, 'Asia/Ho_Chi_Minh'),
('33333333-3333-3333-3333-333333333333', 'DN-BI', 'Trung tâm Y tế His.Hope Đà Nẵng',
 'His.Hope Da Nang Medical Center', '789 Đường Bạch Đằng, Quận Hải Châu, Đà Nẵng',
 '0236.9876.5432', 'MedicalCenter', true, 'Asia/Ho_Chi_Minh'),
('44444444-4444-4444-4444-444444444444', 'CT-TP', 'Phòng khám His.Hope Cần Thơ',
 'His.Hope Can Tho Clinic', '321 Đường Nguyễn Văn Cừ, Quận Ninh Kiều, Cần Thơ',
 '0292.3456.7890', 'Clinic', true, 'Asia/Ho_Chi_Minh')
ON CONFLICT (id) DO NOTHING;

-- ============================================================================
-- SECTION 6: SEED DATA — Update existing users with extended profile fields
-- ============================================================================

UPDATE "AspNetUsers" SET
    "FirstName" = 'Quản Trị Viên',
    "LastName" = 'Hệ Thống',
    role = 'Admin',
    facilityid = '11111111-1111-1111-1111-111111111111',
    "SecurityStamp" = gen_random_uuid()::VARCHAR,
    "ConcurrencyStamp" = gen_random_uuid()::VARCHAR
WHERE id = '00000000-0000-0000-0000-000000000101'
  AND ("FirstName" IS NULL OR "FirstName" = '');

UPDATE "AspNetUsers" SET
    "FirstName" = 'Minh',
    "LastName" = 'Nguyễn',
    "MiddleName" = 'Văn',
    fullname = 'Nguyễn Văn Minh',
    role = 'Provider',
    "LicenseNumber" = 'BSCK-2020-001',
    "Specialty" = 'Nội tổng quát',
    facilityid = '11111111-1111-1111-1111-111111111111',
    "SecurityStamp" = gen_random_uuid()::VARCHAR,
    "ConcurrencyStamp" = gen_random_uuid()::VARCHAR
WHERE id = '00000000-0000-0000-0000-000000000102'
  AND ("FirstName" IS NULL OR "FirstName" = '');

UPDATE "AspNetUsers" SET
    "FirstName" = 'Lan',
    "LastName" = 'Trần',
    "MiddleName" = 'Thị',
    fullname = 'Trần Thị Lan',
    role = 'Provider',
    "LicenseNumber" = 'BSCK-2021-002',
    "Specialty" = 'Nhi khoa',
    facilityid = '11111111-1111-1111-1111-111111111111',
    "SecurityStamp" = gen_random_uuid()::VARCHAR,
    "ConcurrencyStamp" = gen_random_uuid()::VARCHAR
WHERE id = '00000000-0000-0000-0000-000000000103'
  AND ("FirstName" IS NULL OR "FirstName" = '');

UPDATE "AspNetUsers" SET
    "FirstName" = 'Hồng',
    "LastName" = 'Lê',
    "MiddleName" = 'Thị',
    fullname = 'Lê Thị Hồng',
    role = 'Nurse',
    "LicenseNumber" = 'DD-2019-003',
    facilityid = '11111111-1111-1111-1111-111111111111',
    "SecurityStamp" = gen_random_uuid()::VARCHAR,
    "ConcurrencyStamp" = gen_random_uuid()::VARCHAR
WHERE id = '00000000-0000-0000-0000-000000000104'
  AND ("FirstName" IS NULL OR "FirstName" = '');

-- ============================================================================
-- SECTION 7: SEED DATA — Create BillingClerk role (7th role)
-- ============================================================================

INSERT INTO "AspNetRoles" (id, name, normalizedname, description, issystem, createdat) VALUES
('00000000-0000-0000-0000-000000000007', 'BillingClerk', 'BILLINGCLERK',
 'Nhân viên thanh toán — quản lý hóa đơn, thu ngân, xử lý thanh toán',
 true, now())
ON CONFLICT (id) DO NOTHING;

-- Assign BillingClerk permissions
INSERT INTO "RolePermissions" (roleid, permissioncode)
SELECT '00000000-0000-0000-0000-000000000007', p.code FROM "Permissions" p
WHERE p.code IN (
    '"Patients".read',
    '"Appointments".read',
    'billing.read', 'billing.write',
    'settings.read'
)
ON CONFLICT (roleid, permissioncode) DO NOTHING;

-- ============================================================================
-- SECTION 8: SEED DATA — Additional enriched permissions (comprehensive RBAC)
-- ============================================================================
-- These extend the basic permissions from pg-010 with more granular access.

INSERT INTO "Permissions" (code, name, "Group", description, issystem) VALUES
-- Patient module - enriched
('patients.export', 'Xuất dữ liệu bệnh nhân', 'Bệnh nhân', 'Xuất danh sách bệnh nhân ra Excel/PDF', true),
('patients.merge', 'Hợp nhất hồ sơ bệnh nhân', 'Bệnh nhân', 'Hợp nhất các hồ sơ bệnh nhân trùng lặp', true),
('patients.audit', 'Xem lịch sử bệnh nhân', 'Bệnh nhân', 'Xem nhật ký thay đổi của bệnh nhân', true),

-- Appointment module - enriched
('appointments.cancel', 'Hủy lịch hẹn', 'Lịch hẹn', 'Hủy lịch hẹn đã đặt', true),
('appointments.checkin', 'Check-in bệnh nhân', 'Lịch hẹn', 'Check-in bệnh nhân đến khám', true),
('appointments.manage', 'Quản lý lịch hẹn', 'Lịch hẹn', 'Quản lý toàn bộ module lịch hẹn', true),

-- Clinical module - enriched
('clinical.sign', 'Ký xác nhận khám bệnh', 'Lâm sàng', 'Ký số xác nhận hoàn thành khám bệnh', true),
('clinical.delete', 'Xóa hồ sơ khám bệnh', 'Lâm sàng', 'Xóa phiếu khám bệnh (cần quyền đặc biệt)', true),
('clinical.manage', 'Quản lý lâm sàng', 'Lâm sàng', 'Quản lý toàn bộ module lâm sàng', true),

-- Lab module - enriched
('lab.approve', 'Phê duyệt kết quả', 'Xét nghiệm', 'Phê duyệt kết quả xét nghiệm', true),
('lab.cancel', 'Hủy xét nghiệm', 'Xét nghiệm', 'Hủy yêu cầu xét nghiệm', true),
('lab.manage', 'Quản lý xét nghiệm', 'Xét nghiệm', 'Quản lý toàn bộ module xét nghiệm', true),

-- Billing module - enriched
('billing.void', 'Hủy hóa đơn', 'Tài chính', 'Hủy/Void hóa đơn đã tạo', true),
('billing.pay', 'Thanh toán', 'Tài chính', 'Xử lý thanh toán và thu ngân', true),
('billing.refund', 'Hoàn tiền', 'Tài chính', 'Xử lý hoàn tiền và hủy thanh toán', true),
('billing.report', 'Báo cáo tài chính', 'Tài chính', 'Xem báo cáo doanh thu', true),
('billing.manage', 'Quản lý tài chính', 'Tài chính', 'Quản lý toàn bộ module tài chính', true),

-- Pharmacy module - enriched
('pharmacy.import', 'Nhập kho thuốc', 'Dược', 'Nhập thuốc mới vào kho', true),
('pharmacy.inventory', 'Quản lý tồn kho', 'Dược', 'Kiểm kê và điều chỉnh tồn kho', true),
('pharmacy.cancel', 'Hủy đơn thuốc', 'Dược', 'Hủy đơn thuốc đã kê', true),
('pharmacy.manage', 'Quản lý dược', 'Dược', 'Quản lý toàn bộ module dược', true),

-- Reports module
('reports.view', 'Xem báo cáo', 'Báo cáo', 'Xem báo cáo thống kê y tế', true),
('reports.export', 'Xuất báo cáo', 'Báo cáo', 'Xuất báo cáo ra Excel/PDF', true),
('reports.manage', 'Quản lý báo cáo', 'Báo cáo', 'Tạo và quản lý mẫu báo cáo', true),

-- Admin module - enriched
('admin.audit.read', 'Xem nhật ký hệ thống', 'Quản trị', 'Xem nhật ký kiểm toán hệ thống', true),
('admin.settings.read', 'Xem cài đặt hệ thống', 'Quản trị', 'Xem cài đặt và cấu hình hệ thống', true),
('admin.settings.write', 'Quản lý cài đặt', 'Quản trị', 'Thay đổi cài đặt hệ thống', true),
('admin.facilities.read', 'Xem cơ sở y tế', 'Quản trị', 'Xem danh sách cơ sở y tế', true),
('admin.facilities.write', 'Quản lý cơ sở y tế', 'Quản trị', 'Tạo/sửa/xóa cơ sở y tế', true)
ON CONFLICT (code) DO NOTHING;

-- ============================================================================
-- SECTION 9: SEED DATA — Assign enriched permissions to existing roles
-- ============================================================================

-- Admin: get ALL new permissions
INSERT INTO "RolePermissions" (roleid, permissioncode)
SELECT '00000000-0000-0000-0000-000000000001', p.code FROM "Permissions" p
WHERE p.code LIKE '%.export' OR p.code LIKE '%.manage'
   OR p.code LIKE 'admin.%' OR p.code LIKE 'reports.%'
   OR p.code IN ('patients.audit', 'patients.merge',
                   'appointments.cancel', 'appointments.checkin', 'appointments.manage',
                   'clinical.sign', 'clinical.delete', 'clinical.manage',
                   'lab.approve', 'lab.cancel', 'lab.manage',
                   'billing.void', 'billing.pay', 'billing.refund', 'billing.report', 'billing.manage',
                   'pharmacy.import', 'pharmacy.inventory', 'pharmacy.cancel', 'pharmacy.manage')
ON CONFLICT (roleid, permissioncode) DO NOTHING;

-- Provider: grant clinical + lab + pharmacy enrichments
INSERT INTO "RolePermissions" (roleid, permissioncode)
SELECT '00000000-0000-0000-0000-000000000002', p.code FROM "Permissions" p
WHERE p.code IN (
    'patients.export',
    'appointments.cancel', 'appointments.checkin',
    'clinical.sign',
    'lab.approve',
    'pharmacy.import', 'pharmacy.inventory',
    'reports.view'
)
ON CONFLICT (roleid, permissioncode) DO NOTHING;

-- Nurse: grant checkin + view permissions
INSERT INTO "RolePermissions" (roleid, permissioncode)
SELECT '00000000-0000-0000-0000-000000000003', p.code FROM "Permissions" p
WHERE p.code IN (
    'appointments.checkin',
    'reports.view'
)
ON CONFLICT (roleid, permissioncode) DO NOTHING;

-- Receptionist: grant cancel + checkin
INSERT INTO "RolePermissions" (roleid, permissioncode)
SELECT '00000000-0000-0000-0000-000000000004', p.code FROM "Permissions" p
WHERE p.code IN (
    'appointments.cancel', 'appointments.checkin'
)
ON CONFLICT (roleid, permissioncode) DO NOTHING;

-- LabTechnician: grant lab approve
INSERT INTO "RolePermissions" (roleid, permissioncode)
SELECT '00000000-0000-0000-0000-000000000005', p.code FROM "Permissions" p
WHERE p.code IN (
    'lab.approve'
)
ON CONFLICT (roleid, permissioncode) DO NOTHING;

-- Pharmacist: grant pharmacy enrichments
INSERT INTO "RolePermissions" (roleid, permissioncode)
SELECT '00000000-0000-0000-0000-000000000006', p.code FROM "Permissions" p
WHERE p.code IN (
    'pharmacy.import', 'pharmacy.inventory', 'pharmacy.cancel'
)
ON CONFLICT (roleid, permissioncode) DO NOTHING;

-- ============================================================================
-- SECTION 10: SEED DATA — Additional system settings
-- ============================================================================

INSERT INTO "SystemSettings" ("Key", value, description, category, updatedat) VALUES
('hospital.facilities', 'HCM-HQ,HN-BR,DN-BI,CT-TP', 'Danh sách mã cơ sở y tế đang hoạt động', 'hospital', now()),
('hospital.defaultTimezone', 'Asia/Ho_Chi_Minh', 'Múi giờ mặc định của bệnh viện', 'hospital', now()),
('system.enableRefreshTokenRotation', 'true', 'Bật/tắt refresh token rotation (family-based)', 'system', now()),
('system.maxLoginAttempts', '5', 'Số lần đăng nhập sai tối đa trước khi khóa', 'system', now()),
('system.passwordMinLength', '8', 'Độ dài tối thiểu của mật khẩu', 'system', now()),
('system.passwordRequireSpecialChar', 'true', 'Mật khẩu yêu cầu ký tự đặc biệt', 'system', now()),
('clinical.defaultEncounterType', 'Consultation', 'Loại khám mặc định khi tạo phiếu khám mới', 'clinical', now()),
('clinical.maxDiagnosesPerEncounter', '10', 'Số chẩn đoán tối đa cho một lần khám', 'clinical', now()),
('billing.defaultPaymentTerms', '15', 'Thời hạn thanh toán mặc định (ngày)', 'billing', now()),
('billing.enablePartialPayment', 'true', 'Cho phép thanh toán một phần', 'billing', now()),
('pharmacy.defaultStockAlert', '50', 'Ngưỡng cảnh báo tồn kho tối thiểu', 'pharmacy', now()),
('pharmacy.enableGenericSubstitution', 'true', 'Cho phép thay thế thuốc gốc bằng thuốc generic', 'pharmacy', now()),
('lab.autoApproveNormalResults', 'true', 'Tự động phê duyệt kết quả xét nghiệm bình thường', 'lab', now()),
('lab.maxOrdersPerDay', '200', 'Số lượng xét nghiệm tối đa mỗi ngày', 'lab', now()),
('appointment.defaultDuration', '30', 'Thời gian mặc định cho một lịch hẹn (phút)', 'appointment', now()),
('appointment.maxAdvanceDays', '90', 'Số ngày tối đa có thể đặt lịch hẹn trước', 'appointment', now())
ON CONFLICT ("Key") DO NOTHING;

-- ============================================================================
-- SECTION 11: SEED DATA — Sample refresh tokens for seeded users
-- ============================================================================

INSERT INTO "RefreshTokenStore" (id, userid, tokenhash, familyid, deviceinfo, ipaddress, createdat, expiresat) VALUES
('00000000-0000-0000-0000-000000000901',
 '00000000-0000-0000-0000-000000000101',
 encode(gen_random_bytes(32), 'hex'), 'fam-' || gen_random_uuid()::VARCHAR,
 '{"device":"Server","os":"Linux","browser":"System"}',
 '127.0.0.1', '2026-07-01 08:00:00+00', '2026-08-01 08:00:00+00'),
('00000000-0000-0000-0000-000000000902',
 '00000000-0000-0000-0000-000000000102',
 encode(gen_random_bytes(32), 'hex'), 'fam-' || gen_random_uuid()::VARCHAR,
 '{"device":"Desktop","os":"Windows 11","browser":"Chrome 125"}',
 '192.168.1.100', '2026-07-15 08:05:00+00', '2026-08-15 08:05:00+00'),
('00000000-0000-0000-0000-000000000903',
 '00000000-0000-0000-0000-000000000103',
 encode(gen_random_bytes(32), 'hex'), 'fam-' || gen_random_uuid()::VARCHAR,
 '{"device":"Laptop","os":"macOS 14","browser":"Safari 17"}',
 '192.168.1.101', '2026-07-15 09:00:00+00', '2026-08-15 09:00:00+00')
ON CONFLICT (id) DO NOTHING;

-- ============================================================================
-- SECTION 12: Update existing seed data Patients with FacilityId & CreatedBy
-- ============================================================================

\c patientdb

UPDATE "Patients" SET
    "FacilityId" = '11111111-1111-1111-1111-111111111111',
    "CreatedBy" = '00000000-0000-0000-0000-000000000101'
WHERE "FacilityId" IS NULL;

-- ============================================================================
-- SECTION 13: Update existing seed Encounters with FacilityId
-- ============================================================================

\c clinicaldb

UPDATE "Encounters" SET
    "FacilityId" = '11111111-1111-1111-1111-111111111111'
WHERE "FacilityId" IS NULL;

-- Switch back
\c postgres

-- ============================================================================
-- SECTION 14: Verification queries
-- ============================================================================
-- After running this migration, verify with:
--
--   -- Facilities
--   SELECT Code, Name, "FacilityType" FROM identitydb."Facilities";
--
--   -- Count total permissions
--   SELECT COUNT(*) AS total_permissions FROM identitydb."Permissions";
--
--   -- Count all roles
--   SELECT r."Name", COUNT(rp."PermissionCode") AS permissions
--   FROM identitydb."AspNetRoles" r
--   LEFT JOIN identitydb."RolePermissions" rp ON r."Id"::VARCHAR = rp."RoleId"
--   GROUP BY r."Name", r."Id" ORDER BY r."Name";
--
--   -- Enriched user profiles
--   SELECT u."UserName", u."FullName", u."Role", u."LicenseNumber", u."Specialty"
--   FROM identitydb."AspNetUsers" u WHERE u."Role" = 'Provider';
--
--   -- Refresh tokens count
--   SELECT COUNT(*) AS refresh_tokens FROM identitydb."RefreshTokenStore";
-- ============================================================================

-- ============================================================================
-- END OF MIGRATION pg-013
-- ============================================================================
