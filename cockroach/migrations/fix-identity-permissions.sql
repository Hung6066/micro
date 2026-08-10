-- ============================================================================
-- Fix Identity Permission Codes (for PostgreSQL / snake_case)
-- ============================================================================
-- Problem: Existing seed data used wrong permission codes like "Patients".read
-- instead of patients.view (as expected by the C# PermissionHandler).
-- 
-- This script:
-- 1. Removes old wrong permission codes and their role assignments
-- 2. Inserts correct permission codes matching HisHopePermissions constants
-- 3. Assigns admin user to Admin role (not Provider)
-- 4. Verifies final state
-- ============================================================================

BEGIN;

-- Step 1: Remove old role_permissions entries with wrong codes
DELETE FROM role_permissions
WHERE permission_code NOT IN (
    'patients.view', 'patients.create', 'patients.update', 'patients.delete',
    'patients.export', 'patients.manage',
    'appointments.view', 'appointments.create', 'appointments.update',
    'appointments.cancel', 'appointments.check-in', 'appointments.manage',
    'clinical.view', 'clinical.create', 'clinical.update', 'clinical.sign',
    'clinical.delete', 'clinical.manage',
    'lab.view', 'lab.create', 'lab.update', 'lab.result',
    'lab.approve', 'lab.cancel', 'lab.manage',
    'billing.view', 'billing.create', 'billing.update', 'billing.void',
    'billing.pay', 'billing.manage',
    'pharmacy.view', 'pharmacy.create', 'pharmacy.update', 'pharmacy.dispense',
    'pharmacy.cancel', 'pharmacy.manage',
    'admin.users.read', 'admin.users.write', 'admin.roles.read', 'admin.roles.write',
    'admin.permissions.read', 'admin.permissions.write',
    'admin.settings.read', 'admin.settings.write', 'admin.audit.read',
    'reports.view', 'reports.export', 'reports.manage'
);

-- Step 2: Remove old permissions with wrong codes
DELETE FROM permissions
WHERE code NOT IN (
    'patients.view', 'patients.create', 'patients.update', 'patients.delete',
    'patients.export', 'patients.manage',
    'appointments.view', 'appointments.create', 'appointments.update',
    'appointments.cancel', 'appointments.check-in', 'appointments.manage',
    'clinical.view', 'clinical.create', 'clinical.update', 'clinical.sign',
    'clinical.delete', 'clinical.manage',
    'lab.view', 'lab.create', 'lab.update', 'lab.result',
    'lab.approve', 'lab.cancel', 'lab.manage',
    'billing.view', 'billing.create', 'billing.update', 'billing.void',
    'billing.pay', 'billing.manage',
    'pharmacy.view', 'pharmacy.create', 'pharmacy.update', 'pharmacy.dispense',
    'pharmacy.cancel', 'pharmacy.manage',
    'admin.users.read', 'admin.users.write', 'admin.roles.read', 'admin.roles.write',
    'admin.permissions.read', 'admin.permissions.write',
    'admin.settings.read', 'admin.settings.write', 'admin.audit.read',
    'reports.view', 'reports.export', 'reports.manage'
);

-- Step 3: Insert correct permissions (idempotent)
INSERT INTO permissions (code, name, "group", description, is_system, created_at) VALUES

-- Patients
('patients.view', 'Xem bệnh nhân', 'Bệnh nhân', 'Xem thông tin bệnh nhân', true, now()),
('patients.create', 'Thêm bệnh nhân', 'Bệnh nhân', 'Tạo hồ sơ bệnh nhân mới', true, now()),
('patients.update', 'Cập nhật bệnh nhân', 'Bệnh nhân', 'Chỉnh sửa thông tin bệnh nhân', true, now()),
('patients.delete', 'Xóa bệnh nhân', 'Bệnh nhân', 'Xóa hồ sơ bệnh nhân', true, now()),
('patients.export', 'Xuất dữ liệu bệnh nhân', 'Bệnh nhân', 'Xuất danh sách bệnh nhân', true, now()),
('patients.manage', 'Quản lý bệnh nhân', 'Bệnh nhân', 'Toàn quyền quản lý bệnh nhân', true, now()),

-- Appointments
('appointments.view', 'Xem lịch hẹn', 'Lịch hẹn', 'Xem lịch hẹn khám bệnh', true, now()),
('appointments.create', 'Tạo lịch hẹn', 'Lịch hẹn', 'Đặt lịch hẹn mới', true, now()),
('appointments.update', 'Cập nhật lịch hẹn', 'Lịch hẹn', 'Chỉnh sửa lịch hẹn', true, now()),
('appointments.cancel', 'Hủy lịch hẹn', 'Lịch hẹn', 'Hủy lịch hẹn khám', true, now()),
('appointments.check-in', 'Check-in lịch hẹn', 'Lịch hẹn', 'Xác nhận bệnh nhân đến khám', true, now()),
('appointments.manage', 'Quản lý lịch hẹn', 'Lịch hẹn', 'Toàn quyền quản lý lịch hẹn', true, now()),

-- Clinical
('clinical.view', 'Xem hồ sơ bệnh án', 'Lâm sàng', 'Xem hồ sơ bệnh án', true, now()),
('clinical.create', 'Tạo hồ sơ bệnh án', 'Lâm sàng', 'Tạo hồ sơ khám bệnh mới', true, now()),
('clinical.update', 'Cập nhật hồ sơ', 'Lâm sàng', 'Cập nhật thông tin khám bệnh', true, now()),
('clinical.sign', 'Ký hồ sơ bệnh án', 'Lâm sàng', 'Ký điện tử hồ sơ bệnh án', true, now()),
('clinical.delete', 'Xóa hồ sơ', 'Lâm sàng', 'Xóa hồ sơ khám bệnh', true, now()),
('clinical.manage', 'Quản lý lâm sàng', 'Lâm sàng', 'Toàn quyền quản lý lâm sàng', true, now()),

-- Lab Orders
('lab.view', 'Xem xét nghiệm', 'Xét nghiệm', 'Xem phiếu xét nghiệm', true, now()),
('lab.create', 'Tạo xét nghiệm', 'Xét nghiệm', 'Tạo phiếu xét nghiệm mới', true, now()),
('lab.update', 'Cập nhật xét nghiệm', 'Xét nghiệm', 'Cập nhật thông tin xét nghiệm', true, now()),
('lab.result', 'Nhập kết quả', 'Xét nghiệm', 'Nhập kết quả xét nghiệm', true, now()),
('lab.approve', 'Phê duyệt kết quả', 'Xét nghiệm', 'Phê duyệt kết quả xét nghiệm', true, now()),
('lab.cancel', 'Hủy xét nghiệm', 'Xét nghiệm', 'Hủy phiếu xét nghiệm', true, now()),
('lab.manage', 'Quản lý xét nghiệm', 'Xét nghiệm', 'Toàn quyền quản lý xét nghiệm', true, now()),

-- Billing
('billing.view', 'Xem hóa đơn', 'Thanh toán', 'Xem hóa đơn thanh toán', true, now()),
('billing.create', 'Tạo hóa đơn', 'Thanh toán', 'Tạo hóa đơn mới', true, now()),
('billing.update', 'Cập nhật hóa đơn', 'Thanh toán', 'Cập nhật thông tin hóa đơn', true, now()),
('billing.void', 'Hủy hóa đơn', 'Thanh toán', 'Hủy hóa đơn', true, now()),
('billing.pay', 'Thanh toán', 'Thanh toán', 'Ghi nhận thanh toán', true, now()),
('billing.manage', 'Quản lý thanh toán', 'Thanh toán', 'Toàn quyền quản lý thanh toán', true, now()),

-- Pharmacy
('pharmacy.view', 'Xem thuốc', 'Dược phẩm', 'Xem thông tin thuốc và đơn thuốc', true, now()),
('pharmacy.create', 'Thêm thuốc', 'Dược phẩm', 'Thêm thuốc mới vào danh mục', true, now()),
('pharmacy.update', 'Cập nhật thuốc', 'Dược phẩm', 'Cập nhật thông tin thuốc', true, now()),
('pharmacy.dispense', 'Cấp phát thuốc', 'Dược phẩm', 'Cấp phát thuốc cho bệnh nhân', true, now()),
('pharmacy.cancel', 'Hủy đơn thuốc', 'Dược phẩm', 'Hủy đơn thuốc', true, now()),
('pharmacy.manage', 'Quản lý dược phẩm', 'Dược phẩm', 'Toàn quyền quản lý dược phẩm', true, now()),

-- Admin
('admin.users.read', 'Xem người dùng', 'Quản trị', 'Xem danh sách người dùng', true, now()),
('admin.users.write', 'Quản lý người dùng', 'Quản trị', 'Tạo/sửa/xóa người dùng', true, now()),
('admin.roles.read', 'Xem vai trò', 'Quản trị', 'Xem danh sách vai trò', true, now()),
('admin.roles.write', 'Quản lý vai trò', 'Quản trị', 'Tạo/sửa/xóa vai trò', true, now()),
('admin.permissions.read', 'Xem quyền', 'Quản trị', 'Xem danh sách quyền', true, now()),
('admin.permissions.write', 'Quản lý quyền', 'Quản trị', 'Gán quyền cho vai trò', true, now()),
('admin.settings.read', 'Xem cài đặt', 'Quản trị', 'Xem cài đặt hệ thống', true, now()),
('admin.settings.write', 'Quản lý cài đặt', 'Quản trị', 'Thay đổi cài đặt hệ thống', true, now()),
('admin.audit.read', 'Xem nhật ký', 'Quản trị', 'Xem nhật ký hệ thống', true, now()),

-- Reports
('reports.view', 'Xem báo cáo', 'Báo cáo', 'Xem báo cáo thống kê', true, now()),
('reports.export', 'Xuất báo cáo', 'Báo cáo', 'Xuất báo cáo ra file', true, now()),
('reports.manage', 'Quản lý báo cáo', 'Báo cáo', 'Toàn quyền quản lý báo cáo', true, now())

ON CONFLICT (code) DO NOTHING;

-- Step 4: Assign ALL permissions to Admin role
INSERT INTO role_permissions (role_id, permission_code)
SELECT '00000000-0000-0000-0000-000000000001', code FROM permissions
ON CONFLICT (role_id, permission_code) DO NOTHING;

-- Step 5: Assign Provider permissions
INSERT INTO role_permissions (role_id, permission_code)
SELECT '00000000-0000-0000-0000-000000000002', code FROM permissions
WHERE code IN (
    'patients.view', 'patients.create', 'patients.update',
    'appointments.view', 'appointments.create', 'appointments.update', 'appointments.cancel',
    'clinical.view', 'clinical.create', 'clinical.update', 'clinical.sign',
    'lab.view', 'lab.create',
    'pharmacy.view', 'pharmacy.create', 'pharmacy.dispense'
)
ON CONFLICT (role_id, permission_code) DO NOTHING;

-- Step 6: Assign Nurse permissions
INSERT INTO role_permissions (role_id, permission_code)
SELECT '00000000-0000-0000-0000-000000000003', code FROM permissions
WHERE code IN (
    'patients.view', 'patients.update',
    'appointments.view', 'appointments.check-in',
    'clinical.view', 'clinical.create', 'clinical.update',
    'lab.view'
)
ON CONFLICT (role_id, permission_code) DO NOTHING;

-- Step 7: Assign Receptionist permissions
INSERT INTO role_permissions (role_id, permission_code)
SELECT '00000000-0000-0000-0000-000000000004', code FROM permissions
WHERE code IN (
    'patients.view', 'patients.create',
    'appointments.view', 'appointments.create', 'appointments.check-in',
    'billing.view', 'billing.create'
)
ON CONFLICT (role_id, permission_code) DO NOTHING;

-- Step 8: Assign LabTechnician permissions
INSERT INTO role_permissions (role_id, permission_code)
SELECT '00000000-0000-0000-0000-000000000005', code FROM permissions
WHERE code IN (
    'lab.view', 'lab.create', 'lab.update', 'lab.result',
    'patients.view'
)
ON CONFLICT (role_id, permission_code) DO NOTHING;

-- Step 9: Assign Pharmacist permissions
INSERT INTO role_permissions (role_id, permission_code)
SELECT '00000000-0000-0000-0000-000000000006', code FROM permissions
WHERE code IN (
    'pharmacy.view', 'pharmacy.update', 'pharmacy.dispense',
    'patients.view'
)
ON CONFLICT (role_id, permission_code) DO NOTHING;

-- Step 10: Assign BillingClerk permissions
INSERT INTO role_permissions (role_id, permission_code)
SELECT '00000000-0000-0000-0000-000000000007', code FROM permissions
WHERE code IN (
    'billing.view', 'billing.create', 'billing.update', 'billing.void',
    'patients.view'
)
ON CONFLICT (role_id, permission_code) DO NOTHING;

-- Step 11: Assign admin user to Admin role (and remove from Provider)
DO $$
DECLARE
    admin_user_id UUID;
    admin_role_id CONSTANT UUID := '00000000-0000-0000-0000-000000000001';
    provider_role_id CONSTANT UUID := '00000000-0000-0000-0000-000000000002';
BEGIN
    -- Find admin user by username
    SELECT id INTO admin_user_id FROM asp_net_users WHERE user_name = 'admin';
    
    IF admin_user_id IS NOT NULL THEN
        -- Remove from Provider role if assigned
        DELETE FROM asp_net_user_roles
        WHERE user_id = admin_user_id AND role_id = provider_role_id;
        
        -- Assign to Admin role if not already
        INSERT INTO asp_net_user_roles (user_id, role_id)
        VALUES (admin_user_id, admin_role_id)
        ON CONFLICT (user_id, role_id) DO NOTHING;
    END IF;
END $$;

-- Step 12: Ensure admin@hishop.vn stays in Admin role
INSERT INTO asp_net_user_roles (user_id, role_id)
SELECT id, '00000000-0000-0000-0000-000000000001'
FROM asp_net_users
WHERE user_name = 'admin@hishop.vn'
ON CONFLICT (user_id, role_id) DO NOTHING;

COMMIT;

-- ============================================================================
-- Verification Queries
-- ============================================================================
SELECT '--- PERMISSIONS COUNT ---' AS info;
SELECT COUNT(*) AS total FROM permissions;

SELECT '--- ROLE-PERMISSION COUNTS ---' AS info;
SELECT r.name AS role, COUNT(rp.permission_code) AS permission_count
FROM asp_net_roles r
LEFT JOIN role_permissions rp ON rp.role_id = r.id
GROUP BY r.name
ORDER BY r.name;

SELECT '--- USER-ROLE ASSIGNMENTS ---' AS info;
SELECT u.user_name, r.name AS role_name
FROM asp_net_user_roles ur
JOIN asp_net_users u ON u.id = ur.user_id
JOIN asp_net_roles r ON r.id = ur.role_id
ORDER BY u.user_name;

SELECT '--- ADMIN PERMISSIONS SAMPLE ---' AS info;
SELECT rp.permission_code
FROM role_permissions rp
WHERE rp.role_id = '00000000-0000-0000-0000-000000000001'
ORDER BY rp.permission_code;
