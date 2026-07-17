-- ============================================================================
-- His.Hope Identity Seed Data (for Docker PostgreSQL)
-- Combines pg-010 + pg-009 + pg-013 seed sections
-- Idempotent: uses ON CONFLICT DO NOTHING for all inserts.
-- ============================================================================

BEGIN;

-- ============================================================================
-- SECTION 1: Seed Permissions (from pg-010)
-- ============================================================================
INSERT INTO "Permissions" ("Code", "Name", "Group", Description, IsSystem) VALUES
('"Patients".read', 'Xem bệnh nhân', 'Bệnh nhân', 'Xem thông tin bệnh nhân', true),
('"Patients".write', 'Cập nhật bệnh nhân', 'Bệnh nhân', 'Cập nhật thông tin bệnh nhân', true),
('"Patients".delete', 'Xóa bệnh nhân', 'Bệnh nhân', 'Xóa bệnh nhân khỏi hệ thống', true),
('"Appointments".read', 'Xem lịch hẹn', 'Lịch hẹn', 'Xem lịch hẹn khám bệnh', true),
('"Appointments".write', 'Quản lý lịch hẹn', 'Lịch hẹn', 'Tạo và cập nhật lịch hẹn', true),
('clinical.read', 'Xem hồ sơ bệnh án', 'Lâm sàng', 'Xem hồ sơ bệnh án, toa thuốc', true),
('clinical.write', 'Ghi chép lâm sàng', 'Lâm sàng', 'Ghi chép kết quả khám, SOAP, toa thuốc', true),
('lab.read', 'Xem xét nghiệm', 'Xét nghiệm', 'Xem kết quả xét nghiệm', true),
('lab.write', 'Chỉ định xét nghiệm', 'Xét nghiệm', 'Tạo yêu cầu xét nghiệm', true),
('lab.result', 'Nhập kết quả xét nghiệm', 'Xét nghiệm', 'Nhập và xác nhận kết quả xét nghiệm', true),
('pharmacy.read', 'Xem kho thuốc', 'Dược', 'Xem thông tin thuốc và tồn kho', true),
('pharmacy.write', 'Quản lý thuốc', 'Dược', 'Cập nhật thông tin thuốc', true),
('pharmacy.dispense', 'Cấp phát thuốc', 'Dược', 'Cấp phát thuốc theo toa', true),
('billing.read', 'Xem hóa đơn', 'Tài chính', 'Xem hóa đơn và thanh toán', true),
('billing.write', 'Quản lý hóa đơn', 'Tài chính', 'Tạo và cập nhật hóa đơn', true),
('users.read', 'Xem người dùng', 'Người dùng', 'Xem danh sách người dùng', true),
('users.write', 'Quản lý người dùng', 'Người dùng', 'Tạo và cập nhật người dùng', true),
('users.manage_roles', 'Quản lý vai trò', 'Người dùng', 'Gán vai trò và phân quyền', true),
('settings.read', 'Xem cấu hình', 'Cấu hình', 'Xem cấu hình hệ thống', true),
('settings.write', 'Cập nhật cấu hình', 'Cấu hình', 'Cập nhật cấu hình hệ thống', true),
('audit.read', 'Xem nhật ký', 'Kiểm toán', 'Xem nhật ký truy cập PHI', true),
-- Additional permissions from pg-013
('patients.export', 'Xuất dữ liệu bệnh nhân', 'Bệnh nhân', 'Xuất danh sách bệnh nhân ra Excel/PDF', true),
('patients.merge', 'Hợp nhất hồ sơ bệnh nhân', 'Bệnh nhân', 'Hợp nhất các hồ sơ bệnh nhân trùng lặp', true),
('patients.audit', 'Xem lịch sử bệnh nhân', 'Bệnh nhân', 'Xem nhật ký thay đổi của bệnh nhân', true),
('appointments.cancel', 'Hủy lịch hẹn', 'Lịch hẹn', 'Hủy lịch hẹn đã đặt', true),
('appointments.checkin', 'Check-in bệnh nhân', 'Lịch hẹn', 'Check-in bệnh nhân đến khám', true),
('appointments.manage', 'Quản lý lịch hẹn', 'Lịch hẹn', 'Quản lý toàn bộ module lịch hẹn', true),
('clinical.sign', 'Ký xác nhận khám bệnh', 'Lâm sàng', 'Ký số xác nhận hoàn thành khám bệnh', true),
('clinical.delete', 'Xóa hồ sơ khám bệnh', 'Lâm sàng', 'Xóa phiếu khám bệnh', true),
('clinical.manage', 'Quản lý lâm sàng', 'Lâm sàng', 'Quản lý toàn bộ module lâm sàng', true),
('lab.approve', 'Phê duyệt kết quả', 'Xét nghiệm', 'Phê duyệt kết quả xét nghiệm', true),
('lab.cancel', 'Hủy xét nghiệm', 'Xét nghiệm', 'Hủy yêu cầu xét nghiệm', true),
('lab.manage', 'Quản lý xét nghiệm', 'Xét nghiệm', 'Quản lý toàn bộ module xét nghiệm', true),
('billing.void', 'Hủy hóa đơn', 'Tài chính', 'Hủy/Void hóa đơn đã tạo', true),
('billing.pay', 'Thanh toán', 'Tài chính', 'Xử lý thanh toán và thu ngân', true),
('billing.refund', 'Hoàn tiền', 'Tài chính', 'Xử lý hoàn tiền và hủy thanh toán', true),
('billing.report', 'Báo cáo tài chính', 'Tài chính', 'Xem báo cáo doanh thu', true),
('billing.manage', 'Quản lý tài chính', 'Tài chính', 'Quản lý toàn bộ module tài chính', true),
('pharmacy.import', 'Nhập kho thuốc', 'Dược', 'Nhập thuốc mới vào kho', true),
('pharmacy.inventory', 'Quản lý tồn kho', 'Dược', 'Kiểm kê và điều chỉnh tồn kho', true),
('pharmacy.cancel', 'Hủy đơn thuốc', 'Dược', 'Hủy đơn thuốc đã kê', true),
('pharmacy.manage', 'Quản lý dược', 'Dược', 'Quản lý toàn bộ module dược', true),
('reports.view', 'Xem báo cáo', 'Báo cáo', 'Xem báo cáo thống kê y tế', true),
('reports.export', 'Xuất báo cáo', 'Báo cáo', 'Xuất báo cáo ra Excel/PDF', true),
('reports.manage', 'Quản lý báo cáo', 'Báo cáo', 'Tạo và quản lý mẫu báo cáo', true),
('admin.audit.read', 'Xem nhật ký hệ thống', 'Quản trị', 'Xem nhật ký kiểm toán hệ thống', true),
('admin.settings.read', 'Xem cài đặt hệ thống', 'Quản trị', 'Xem cài đặt và cấu hình hệ thống', true),
('admin.settings.write', 'Quản lý cài đặt', 'Quản trị', 'Thay đổi cài đặt hệ thống', true),
('admin.facilities.read', 'Xem cơ sở y tế', 'Quản trị', 'Xem danh sách cơ sở y tế', true),
('admin.facilities.write', 'Quản lý cơ sở y tế', 'Quản trị', 'Tạo/sửa/xóa cơ sở y tế', true)
ON CONFLICT ("Code") DO NOTHING;

-- ============================================================================
-- SECTION 2: Seed Roles
-- ============================================================================
INSERT INTO "AspNetRoles" (Id, Name, NormalizedName, Description, IsSystem, CreatedAt, ConcurrencyStamp) VALUES
('00000000-0000-0000-0000-000000000001', 'Admin', 'ADMIN', 'Quản trị viên hệ thống - có toàn quyền', true, now(), gen_random_uuid()::VARCHAR),
('00000000-0000-0000-0000-000000000002', 'Provider', 'PROVIDER', 'Bác sĩ - khám và điều trị', true, now(), gen_random_uuid()::VARCHAR),
('00000000-0000-0000-0000-000000000003', 'Nurse', 'NURSE', 'Điều dưỡng - hỗ trợ khám bệnh', true, now(), gen_random_uuid()::VARCHAR),
('00000000-0000-0000-0000-000000000004', 'Receptionist', 'RECEPTIONIST', 'Lễ tân - tiếp nhận bệnh nhân', true, now(), gen_random_uuid()::VARCHAR),
('00000000-0000-0000-0000-000000000005', 'LabTechnician', 'LABTECHNICIAN', 'Kỹ thuật viên xét nghiệm', true, now(), gen_random_uuid()::VARCHAR),
('00000000-0000-0000-0000-000000000006', 'Pharmacist', 'PHARMACIST', 'Dược sĩ - cấp phát thuốc', true, now(), gen_random_uuid()::VARCHAR),
('00000000-0000-0000-0000-000000000007', 'BillingClerk', 'BILLINGCLERK', 'Nhân viên thanh toán', true, now(), gen_random_uuid()::VARCHAR)
ON CONFLICT ("Id") DO UPDATE SET
    Name = EXCLUDED.Name,
    NormalizedName = EXCLUDED.NormalizedName,
    Description = EXCLUDED.Description;

-- ============================================================================
-- SECTION 3: Assign ALL permissions to Admin role
-- ============================================================================
INSERT INTO "RolePermissions" (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000001', Code FROM "Permissions"
ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

-- Provider permissions
INSERT INTO "RolePermissions" (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000002', Code FROM "Permissions"
WHERE Code IN ('"Patients".read', '"Patients".write',
    '"Appointments".read', '"Appointments".write',
    'clinical.read', 'clinical.write', 'clinical.sign',
    'lab.read', 'lab.write', 'lab.approve',
    'pharmacy.read', 'pharmacy.write', 'pharmacy.dispense',
    'billing.read',
    'patients.export',
    'appointments.cancel', 'appointments.checkin',
    'reports.view')
ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

-- Nurse permissions
INSERT INTO "RolePermissions" (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000003', Code FROM "Permissions"
WHERE Code IN ('"Patients".read', '"Patients".write',
    '"Appointments".read', 'appointments.checkin',
    'clinical.read', 'clinical.write')
ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

-- Receptionist permissions
INSERT INTO "RolePermissions" (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000004', Code FROM "Permissions"
WHERE Code IN ('"Patients".read', '"Patients".write',
    '"Appointments".read', '"Appointments".write',
    'appointments.cancel', 'appointments.checkin')
ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

-- LabTechnician permissions
INSERT INTO "RolePermissions" (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000005', Code FROM "Permissions"
WHERE Code IN ('lab.read', 'lab.write', 'lab.result', 'lab.approve',
    '"Patients".read', '"Appointments".read')
ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

-- Pharmacist permissions
INSERT INTO "RolePermissions" (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000006', Code FROM "Permissions"
WHERE Code IN ('pharmacy.read', 'pharmacy.write', 'pharmacy.dispense',
    'pharmacy.import', 'pharmacy.inventory', 'pharmacy.cancel',
    '"Patients".read', '"Appointments".read')
ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

-- BillingClerk permissions
INSERT INTO "RolePermissions" (RoleId, PermissionCode)
SELECT '00000000-0000-0000-0000-000000000007', Code FROM "Permissions"
WHERE Code IN ('billing.read', 'billing.write', 'billing.void', 'billing.pay',
    'billing.refund', 'billing.report',
    '"Patients".read', '"Appointments".read', 'settings.read')
ON CONFLICT (RoleId, PermissionCode) DO NOTHING;

COMMIT;

-- ============================================================================
-- SECTION 4: System Settings
-- ============================================================================
BEGIN;

INSERT INTO "SystemSettings" ("Key", "Value", Description, Category, UpdatedAt) VALUES
('hospital.name', 'Bệnh viện His.Hope', 'Tên bệnh viện', 'hospital', now()),
('hospital.address', '123 Đường Lê Lợi, Quận 1, TP.HCM', 'Địa chỉ', 'hospital', now()),
('hospital.phone', '028.1234.5678', 'Số điện thoại', 'hospital', now()),
('hospital.email', 'contact@hishop.vn', 'Email', 'hospital', now()),
('system.defaultLanguage', 'vi', 'Ngôn ngữ mặc định', 'system', now()),
('system.sessionTimeout', '480', 'Timeout phiên (phút)', 'system', now()),
('billing.defaultCurrency', 'VND', 'Tiền tệ', 'billing', now()),
('billing.taxRate', '8', 'Thuế suất (%)', 'billing', now())
ON CONFLICT ("Key") DO NOTHING;

COMMIT;

COMMIT;
