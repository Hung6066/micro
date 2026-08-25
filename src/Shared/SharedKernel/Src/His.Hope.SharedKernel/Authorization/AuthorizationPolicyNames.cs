namespace His.Hope.SharedKernel.Authorization;

/// <summary>
/// Compile-time policy names used at resource boundaries. Keep these derived
/// from <see cref="HisHopePermissions"/> so endpoint attributes cannot drift
/// from the permission catalog.
/// </summary>
public static class AuthorizationPolicyNames
{
    public static string Permission(string permissionCode) => $"Permission:{permissionCode}";

    public static class Permissions
    {
        public const string FacilityCross = "Permission:facility.cross";
        public const string PatientsView = "Permission:patients.view";
        public const string PatientsCreate = "Permission:patients.create";
        public const string PatientsUpdate = "Permission:patients.update";
        public const string PatientsDelete = "Permission:patients.delete";
        public const string PatientsExport = "Permission:patients.export";
        public const string PatientsManage = "Permission:patients.manage";
        public const string AppointmentsView = "Permission:appointments.view";
        public const string AppointmentsCreate = "Permission:appointments.create";
        public const string AppointmentsUpdate = "Permission:appointments.update";
        public const string AppointmentsCancel = "Permission:appointments.cancel";
        public const string AppointmentsCheckIn = "Permission:appointments.check-in";
        public const string AppointmentsManage = "Permission:appointments.manage";
        public const string ClinicalView = "Permission:clinical.view";
        public const string ClinicalCreate = "Permission:clinical.create";
        public const string ClinicalUpdate = "Permission:clinical.update";
        public const string ClinicalSign = "Permission:clinical.sign";
        public const string ClinicalDelete = "Permission:clinical.delete";
        public const string ClinicalManage = "Permission:clinical.manage";
        public const string LabView = "Permission:lab.view";
        public const string LabCreate = "Permission:lab.create";
        public const string LabUpdate = "Permission:lab.update";
        public const string LabResult = "Permission:lab.result";
        public const string LabApprove = "Permission:lab.approve";
        public const string LabCancel = "Permission:lab.cancel";
        public const string LabManage = "Permission:lab.manage";
        public const string LabAlertAcknowledge = "Permission:lab.alert.acknowledge";
        public const string LabAlertResolve = "Permission:lab.alert.resolve";
        public const string BillingView = "Permission:billing.view";
        public const string BillingCreate = "Permission:billing.create";
        public const string BillingUpdate = "Permission:billing.update";
        public const string BillingVoid = "Permission:billing.void";
        public const string BillingPay = "Permission:billing.pay";
        public const string BillingManage = "Permission:billing.manage";
        public const string PharmacyView = "Permission:pharmacy.view";
        public const string PharmacyCreate = "Permission:pharmacy.create";
        public const string PharmacyUpdate = "Permission:pharmacy.update";
        public const string PharmacyDispense = "Permission:pharmacy.dispense";
        public const string PharmacyCancel = "Permission:pharmacy.cancel";
        public const string PharmacyManage = "Permission:pharmacy.manage";
        public const string ReportsView = "Permission:reports.view";
        public const string ReportsExport = "Permission:reports.export";
        public const string ReportsManage = "Permission:reports.manage";
        public const string AdminUsersRead = "Permission:admin.users.read";
        public const string AdminUsersWrite = "Permission:admin.users.write";
        public const string AdminRolesRead = "Permission:admin.roles.read";
        public const string AdminRolesWrite = "Permission:admin.roles.write";
        public const string AdminPermissionsRead = "Permission:admin.permissions.read";
        public const string AdminPermissionsWrite = "Permission:admin.permissions.write";
        public const string AdminSettingsRead = "Permission:admin.settings.read";
        public const string AdminSettingsWrite = "Permission:admin.settings.write";
        public const string AdminAuditRead = "Permission:admin.audit.read";
        public const string AdminClientsRead = "Permission:admin.clients.read";
        public const string AdminClientsWrite = "Permission:admin.clients.write";
        public const string AdminBreakGlassRead = "Permission:admin.breakglass.read";
        public const string AdminBreakGlassWrite = "Permission:admin.breakglass.write";
        public const string AdminPolicySimulate = "Permission:admin.policy.simulate";
        public const string AdminSessionsRead = "Permission:admin.sessions.read";
        public const string AdminSessionsRevoke = "Permission:admin.sessions.revoke";
        public const string AdminCredentialsReset = "Permission:admin.credentials.reset";
        public const string AdminProvisioningManage = "Permission:admin.provisioning.manage";
        public const string AdminSecuritySignalsManage = "Permission:admin.security-signals.manage";
        public const string CommerceCatalogView = "Permission:commerce.catalog.view";
        public const string CommerceOrdersCreate = "Permission:commerce.orders.create";
        public const string CommerceOrdersView = "Permission:commerce.orders.view";
        public const string CommerceOrdersUpdate = "Permission:commerce.orders.update";
        public const string CommerceProfileManage = "Permission:commerce.profile.manage";
        public const string CommerceNotificationsView = "Permission:commerce.notifications.view";
    }
}
