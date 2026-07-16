using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace His.Hope.SharedKernel.Authorization;

/// <summary>
/// Central registry of all permission codes in the His.Hope hospital system.
/// Permissions are organized by module and follow the pattern: {module}.{action}
/// NEW PERMISSIONS MUST BE REGISTERED HERE and then seeded in the database.
/// </summary>
public static class HisHopePermissions
{
    // ──────────────────────────────────────────────
    // Patients Module
    // ──────────────────────────────────────────────
    public static class Patients
    {
        public const string View = "patients.view";
        public const string Create = "patients.create";
        public const string Update = "patients.update";
        public const string Delete = "patients.delete";
        public const string Export = "patients.export";
        public const string Manage = "patients.manage";
    }

    // ──────────────────────────────────────────────
    // Appointments Module
    // ──────────────────────────────────────────────
    public static class Appointments
    {
        public const string View = "appointments.view";
        public const string Create = "appointments.create";
        public const string Update = "appointments.update";
        public const string Cancel = "appointments.cancel";
        public const string CheckIn = "appointments.check-in";
        public const string Manage = "appointments.manage";
    }

    // ──────────────────────────────────────────────
    // Clinical / Encounters Module
    // ──────────────────────────────────────────────
    public static class Clinical
    {
        public const string View = "clinical.view";
        public const string Create = "clinical.create";
        public const string Update = "clinical.update";
        public const string Sign = "clinical.sign";
        public const string Delete = "clinical.delete";
        public const string Manage = "clinical.manage";
    }

    // ──────────────────────────────────────────────
    // Lab Orders Module
    // ──────────────────────────────────────────────
    public static class LabOrders
    {
        public const string View = "lab.view";
        public const string Create = "lab.create";
        public const string Update = "lab.update";
        public const string Result = "lab.result";
        public const string Approve = "lab.approve";
        public const string Cancel = "lab.cancel";
        public const string Manage = "lab.manage";
    }

    // ──────────────────────────────────────────────
    // Billing / Invoices Module
    // ──────────────────────────────────────────────
    public static class Billing
    {
        public const string View = "billing.view";
        public const string Create = "billing.create";
        public const string Update = "billing.update";
        public const string Void = "billing.void";
        public const string Pay = "billing.pay";
        public const string Manage = "billing.manage";
    }

    // ──────────────────────────────────────────────
    // Pharmacy / Medications Module
    // ──────────────────────────────────────────────
    public static class Pharmacy
    {
        public const string View = "pharmacy.view";
        public const string Create = "pharmacy.create";
        public const string Update = "pharmacy.update";
        public const string Dispense = "pharmacy.dispense";
        public const string Cancel = "pharmacy.cancel";
        public const string Manage = "pharmacy.manage";
    }

    // ──────────────────────────────────────────────
    // Admin / Identity Module
    // ──────────────────────────────────────────────
    public static class Admin
    {
        public const string UsersRead = "admin.users.read";
        public const string UsersWrite = "admin.users.write";
        public const string RolesRead = "admin.roles.read";
        public const string RolesWrite = "admin.roles.write";
        public const string PermissionsRead = "admin.permissions.read";
        public const string PermissionsWrite = "admin.permissions.write";
        public const string SettingsRead = "admin.settings.read";
        public const string SettingsWrite = "admin.settings.write";
        public const string AuditRead = "admin.audit.read";
    }

    // ──────────────────────────────────────────────
    // Reports Module
    // ──────────────────────────────────────────────
    public static class Reports
    {
        public const string View = "reports.view";
        public const string Export = "reports.export";
        public const string Manage = "reports.manage";
    }

    // ──────────────────────────────────────────────
    // All Permissions - for enumeration and seeding
    // ──────────────────────────────────────────────

    /// <summary>
    /// Complete list of all permission codes in the system.
    /// Used for seeding, validation, and enumeration.
    /// </summary>
    public static readonly FrozenSet<string> All = new[]
    {
        // Patients
        Patients.View, Patients.Create, Patients.Update, Patients.Delete,
        Patients.Export, Patients.Manage,

        // Appointments
        Appointments.View, Appointments.Create, Appointments.Update,
        Appointments.Cancel, Appointments.CheckIn, Appointments.Manage,

        // Clinical / Encounters
        Clinical.View, Clinical.Create, Clinical.Update, Clinical.Sign,
        Clinical.Delete, Clinical.Manage,

        // Lab Orders
        LabOrders.View, LabOrders.Create, LabOrders.Update, LabOrders.Result,
        LabOrders.Approve, LabOrders.Cancel, LabOrders.Manage,

        // Billing
        Billing.View, Billing.Create, Billing.Update, Billing.Void,
        Billing.Pay, Billing.Manage,

        // Pharmacy
        Pharmacy.View, Pharmacy.Create, Pharmacy.Update, Pharmacy.Dispense,
        Pharmacy.Cancel, Pharmacy.Manage,

        // Admin
        Admin.UsersRead, Admin.UsersWrite, Admin.RolesRead, Admin.RolesWrite,
        Admin.PermissionsRead, Admin.PermissionsWrite,
        Admin.SettingsRead, Admin.SettingsWrite, Admin.AuditRead,

        // Reports
        Reports.View, Reports.Export, Reports.Manage,
    }.ToFrozenSet();

    /// <summary>
    /// All permissions grouped by module with display names and descriptions.
    /// Used for UI rendering and seed data generation.
    /// </summary>
    public static readonly FrozenSet<PermissionDescriptor> AllDescriptors = new PermissionDescriptor[]
    {
        // Patients
        new(Patients.View, "Xem bệnh nhân", "Bệnh nhân", "Xem thông tin bệnh nhân"),
        new(Patients.Create, "Thêm bệnh nhân", "Bệnh nhân", "Tạo hồ sơ bệnh nhân mới"),
        new(Patients.Update, "Cập nhật bệnh nhân", "Bệnh nhân", "Chỉnh sửa thông tin bệnh nhân"),
        new(Patients.Delete, "Xóa bệnh nhân", "Bệnh nhân", "Xóa hồ sơ bệnh nhân"),
        new(Patients.Export, "Xuất dữ liệu bệnh nhân", "Bệnh nhân", "Xuất danh sách bệnh nhân"),
        new(Patients.Manage, "Quản lý bệnh nhân", "Bệnh nhân", "Toàn quyền quản lý bệnh nhân"),

        // Appointments
        new(Appointments.View, "Xem lịch hẹn", "Lịch hẹn", "Xem lịch hẹn khám bệnh"),
        new(Appointments.Create, "Tạo lịch hẹn", "Lịch hẹn", "Đặt lịch hẹn mới"),
        new(Appointments.Update, "Cập nhật lịch hẹn", "Lịch hẹn", "Chỉnh sửa lịch hẹn"),
        new(Appointments.Cancel, "Hủy lịch hẹn", "Lịch hẹn", "Hủy lịch hẹn khám"),
        new(Appointments.CheckIn, "Check-in lịch hẹn", "Lịch hẹn", "Xác nhận bệnh nhân đến khám"),
        new(Appointments.Manage, "Quản lý lịch hẹn", "Lịch hẹn", "Toàn quyền quản lý lịch hẹn"),

        // Clinical
        new(Clinical.View, "Xem hồ sơ bệnh án", "Lâm sàng", "Xem hồ sơ bệnh án"),
        new(Clinical.Create, "Tạo hồ sơ bệnh án", "Lâm sàng", "Tạo hồ sơ khám bệnh mới"),
        new(Clinical.Update, "Cập nhật hồ sơ", "Lâm sàng", "Cập nhật thông tin khám bệnh"),
        new(Clinical.Sign, "Ký hồ sơ bệnh án", "Lâm sàng", "Ký điện tử hồ sơ bệnh án"),
        new(Clinical.Delete, "Xóa hồ sơ", "Lâm sàng", "Xóa hồ sơ khám bệnh"),
        new(Clinical.Manage, "Quản lý lâm sàng", "Lâm sàng", "Toàn quyền quản lý lâm sàng"),

        // Lab Orders
        new(LabOrders.View, "Xem xét nghiệm", "Xét nghiệm", "Xem phiếu xét nghiệm"),
        new(LabOrders.Create, "Tạo xét nghiệm", "Xét nghiệm", "Tạo phiếu xét nghiệm mới"),
        new(LabOrders.Update, "Cập nhật xét nghiệm", "Xét nghiệm", "Cập nhật thông tin xét nghiệm"),
        new(LabOrders.Result, "Nhập kết quả", "Xét nghiệm", "Nhập kết quả xét nghiệm"),
        new(LabOrders.Approve, "Phê duyệt kết quả", "Xét nghiệm", "Phê duyệt kết quả xét nghiệm"),
        new(LabOrders.Cancel, "Hủy xét nghiệm", "Xét nghiệm", "Hủy phiếu xét nghiệm"),
        new(LabOrders.Manage, "Quản lý xét nghiệm", "Xét nghiệm", "Toàn quyền quản lý xét nghiệm"),

        // Billing
        new(Billing.View, "Xem hóa đơn", "Thanh toán", "Xem hóa đơn thanh toán"),
        new(Billing.Create, "Tạo hóa đơn", "Thanh toán", "Tạo hóa đơn mới"),
        new(Billing.Update, "Cập nhật hóa đơn", "Thanh toán", "Cập nhật thông tin hóa đơn"),
        new(Billing.Void, "Hủy hóa đơn", "Thanh toán", "Hủy hóa đơn"),
        new(Billing.Pay, "Thanh toán", "Thanh toán", "Ghi nhận thanh toán"),
        new(Billing.Manage, "Quản lý thanh toán", "Thanh toán", "Toàn quyền quản lý thanh toán"),

        // Pharmacy
        new(Pharmacy.View, "Xem thuốc", "Dược phẩm", "Xem thông tin thuốc và đơn thuốc"),
        new(Pharmacy.Create, "Thêm thuốc", "Dược phẩm", "Thêm thuốc mới vào danh mục"),
        new(Pharmacy.Update, "Cập nhật thuốc", "Dược phẩm", "Cập nhật thông tin thuốc"),
        new(Pharmacy.Dispense, "Cấp phát thuốc", "Dược phẩm", "Cấp phát thuốc cho bệnh nhân"),
        new(Pharmacy.Cancel, "Hủy đơn thuốc", "Dược phẩm", "Hủy đơn thuốc"),
        new(Pharmacy.Manage, "Quản lý dược phẩm", "Dược phẩm", "Toàn quyền quản lý dược phẩm"),

        // Admin
        new(Admin.UsersRead, "Xem người dùng", "Quản trị", "Xem danh sách người dùng"),
        new(Admin.UsersWrite, "Quản lý người dùng", "Quản trị", "Tạo/sửa/xóa người dùng"),
        new(Admin.RolesRead, "Xem vai trò", "Quản trị", "Xem danh sách vai trò"),
        new(Admin.RolesWrite, "Quản lý vai trò", "Quản trị", "Tạo/sửa/xóa vai trò"),
        new(Admin.PermissionsRead, "Xem quyền", "Quản trị", "Xem danh sách quyền"),
        new(Admin.PermissionsWrite, "Quản lý quyền", "Quản trị", "Gán quyền cho vai trò"),
        new(Admin.SettingsRead, "Xem cấu hình", "Quản trị", "Xem cấu hình hệ thống"),
        new(Admin.SettingsWrite, "Cập nhật cấu hình", "Quản trị", "Thay đổi cấu hình hệ thống"),
        new(Admin.AuditRead, "Xem nhật ký", "Quản trị", "Xem nhật ký kiểm toán"),

        // Reports
        new(Reports.View, "Xem báo cáo", "Báo cáo", "Xem báo cáo thống kê"),
        new(Reports.Export, "Xuất báo cáo", "Báo cáo", "Xuất báo cáo ra file"),
        new(Reports.Manage, "Quản lý báo cáo", "Báo cáo", "Toàn quyền quản lý báo cáo"),
    }.ToFrozenSet();

    /// <summary>
    /// Returns true if the given permission code is registered in the system.
    /// </summary>
    public static bool IsValid(string permissionCode) => All.Contains(permissionCode);
}

/// <summary>
/// Describes a single permission with its code, display name, group, and description.
/// Used for UI rendering and database seeding.
/// </summary>
public sealed record PermissionDescriptor(
    string Code,
    string Name,
    string Group,
    string? Description = null);
