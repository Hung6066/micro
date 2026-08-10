-- Seed permissions with CreatedAt
INSERT INTO "Permissions" ("Code", "Name", "Group", "Description", "IsSystem", "CreatedAt") VALUES
('"Patients".read', 'Xem bệnh nhân', 'Bệnh nhân', 'Xem thông tin bệnh nhân', true, now()),
('"Patients".write', 'Cập nhật bệnh nhân', 'Bệnh nhân', 'Cập nhật thông tin bệnh nhân', true, now()),
('clinical.read', 'Xem hồ sơ bệnh án', 'Lâm sàng', 'Xem hồ sơ bệnh án', true, now()),
('clinical.write', 'Ghi chép lâm sàng', 'Lâm sàng', 'Ghi chép kết quả khám', true, now()),
('lab.read', 'Xem xét nghiệm', 'Xét nghiệm', 'Xem kết quả xét nghiệm', true, now()),
('lab.write', 'Chỉ định xét nghiệm', 'Xét nghiệm', 'Tạo yêu cầu xét nghiệm', true, now()),
('lab.result', 'Nhập kết quả xét nghiệm', 'Xét nghiệm', 'Nhập kết quả', true, now()),
('pharmacy.read', 'Xem kho thuốc', 'Dược', 'Xem thông tin thuốc', true, now()),
('pharmacy.write', 'Quản lý thuốc', 'Dược', 'Cập nhật thuốc', true, now()),
('pharmacy.dispense', 'Cấp phát thuốc', 'Dược', 'Cấp phát theo toa', true, now()),
('billing.read', 'Xem hóa đơn', 'Tài chính', 'Xem hóa đơn', true, now()),
('billing.write', 'Quản lý hóa đơn', 'Tài chính', 'Tạo và cập nhật hóa đơn', true, now()),
('users.read', 'Xem người dùng', 'Người dùng', 'Xem danh sách người dùng', true, now()),
('users.write', 'Quản lý người dùng', 'Người dùng', 'Tạo và cập nhật người dùng', true, now()),
('users.manage_roles', 'Quản lý vai trò', 'Người dùng', 'Gán vai trò và phân quyền', true, now()),
('settings.read', 'Xem cấu hình', 'Cấu hình', 'Xem cấu hình hệ thống', true, now()),
('settings.write', 'Cập nhật cấu hình', 'Cấu hình', 'Cập nhật cấu hình hệ thống', true, now()),
('audit.read', 'Xem nhật ký', 'Kiểm toán', 'Xem nhật ký truy cập', true, now()),
('reports.view', 'Xem báo cáo', 'Báo cáo', 'Xem báo cáo', true, now())
ON CONFLICT ("Code") DO NOTHING;

-- Create missing roles with CreatedAt
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "Description", "IsSystem", "CreatedAt", "ConcurrencyStamp") VALUES
('00000000-0000-0000-0000-000000000003', 'Nurse', 'NURSE', 'Điều dưỡng', true, now(), gen_random_uuid()::text),
('00000000-0000-0000-0000-000000000004', 'Receptionist', 'RECEPTIONIST', 'Lễ tân', true, now(), gen_random_uuid()::text),
('00000000-0000-0000-0000-000000000005', 'LabTechnician', 'LABTECHNICIAN', 'Kỹ thuật viên xét nghiệm', true, now(), gen_random_uuid()::text),
('00000000-0000-0000-0000-000000000006', 'Pharmacist', 'PHARMACIST', 'Dược sĩ', true, now(), gen_random_uuid()::text),
('00000000-0000-0000-0000-000000000007', 'BillingClerk', 'BILLINGCLERK', 'Nhân viên thanh toán', true, now(), gen_random_uuid()::text)
ON CONFLICT ("Id") DO NOTHING;

-- Assign all permissions to Admin
INSERT INTO "RolePermissions" ("RoleId", "PermissionCode")
SELECT '00000000-0000-0000-0000-000000000001', "Code" FROM "Permissions"
ON CONFLICT ("RoleId", "PermissionCode") DO NOTHING;

-- Assign selected permissions to Provider
INSERT INTO "RolePermissions" ("RoleId", "PermissionCode")
SELECT '00000000-0000-0000-0000-000000000002', "Code" FROM "Permissions"
WHERE "Code" IN ('"Patients".read', '"Patients".write', 'clinical.read', 'clinical.write',
    'lab.read', 'lab.write', 'pharmacy.read', 'billing.read', 'reports.view')
ON CONFLICT ("RoleId", "PermissionCode") DO NOTHING;

-- Assign admin@hishop.vn to Admin role
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT "Id", '00000000-0000-0000-0000-000000000001' FROM "AspNetUsers" WHERE "UserName" = 'admin@hishop.vn'
ON CONFLICT ("UserId", "RoleId") DO NOTHING;

-- Update admin user profile
UPDATE "AspNetUsers" SET
    "FirstName" = 'System',
    "LastName" = 'Admin',
    "EmailConfirmed" = true,
    "IsActive" = true
WHERE "UserName" = 'admin@hishop.vn';

-- Also mark testadmin user as having Provider role
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT "Id", '00000000-0000-0000-0000-000000000002' FROM "AspNetUsers" WHERE "UserName" = 'testadmin'
ON CONFLICT ("UserId", "RoleId") DO NOTHING;

-- Verify results
SELECT COUNT(*) AS total_permissions FROM "Permissions";
SELECT COUNT(*) AS total_roles FROM "AspNetRoles";
SELECT COUNT(*) AS total_rolepermissions FROM "RolePermissions";
SELECT COUNT(*) AS total_userroles FROM "AspNetUserRoles";
SELECT u."UserName", r."Name" AS role_name
FROM "AspNetUserRoles" ur
JOIN "AspNetUsers" u ON u."Id" = ur."UserId"
JOIN "AspNetRoles" r ON r."Id" = ur."RoleId";
