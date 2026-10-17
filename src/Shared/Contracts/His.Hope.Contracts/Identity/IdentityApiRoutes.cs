namespace His.Hope.Contracts.Identity;

/// <summary>
/// Canonical Identity Service HTTP routes. Endpoint registration uses the
/// segment constants; clients and tests use the absolute route constants.
/// Keep route changes here so API and consumers cannot silently drift.
/// </summary>
public static class IdentityApiRoutes
{
    public const string ApiV1 = "/api/v1";
    public const string Auth = ApiV1 + "/auth";
    public const string Login = Auth + "/login";
    public const string Register = Auth + "/register";
    public const string Refresh = Auth + "/refresh";
    public const string InternalRefresh = Auth + "/internal/refresh";
    public const string Logout = Auth + "/logout";
    public const string Verify = Auth + "/verify";
    public const string Me = Auth + "/me";
    public const string SessionStatus = Auth + "/session-status";
    public const string SessionExchange = Auth + "/session/exchange";
    public const string CheckPermission = Auth + "/check-permission";
    public const string ExternalProviders = Auth + "/external-providers";
    public const string ExternalLogin = Auth + "/external-login";
    public const string ExternalCallback = Auth + "/external-callback";
    public const string IdentityLoginScript = Auth + "/identity-login.js";
    public const string Account = Auth + "/account";
    public const string AccountLinkedAccounts = Account + "/linked-accounts";
    public const string Consents = Auth + "/consents";
    public const string LdapLogin = Auth + "/ldap/login";
    public const string Users = Auth + "/users";
    public const string Roles = Auth + "/roles";
    public const string Permissions = Auth + "/permissions";
    public const string Mfa = Auth + "/mfa";
    public const string MfaMethods = Mfa + "/methods";
    public const string MfaEnroll = Mfa + "/enroll";
    public const string MfaVerify = Mfa + "/verify";
    public const string MfaRecover = Mfa + "/recover";
    public const string ForgotPassword = Auth + "/forgot-password";
    public const string ResetPassword = Auth + "/reset-password";
    public const string ChangePassword = Auth + "/change-password";
    public const string SendEmailVerification = Auth + "/send-email-verification";
    public const string VerifyEmail = Auth + "/verify-email";
    public const string Sessions = Auth + "/sessions";
    public const string ForgotPasswordSegment = "/forgot-password";
    public const string ResetPasswordSegment = "/reset-password";
    public const string Settings = ApiV1 + "/settings";
    public const string ScimV2 = "/scim/v2";
    // OpenID Connect protocol endpoints are shared by API clients and tests.
    // Keep them here alongside service routes so protocol URL drift is visible.
    public const string OidcAuthorize = "/connect/authorize";
    public const string OidcToken = "/connect/token";
    public const string OidcIntrospect = "/connect/introspect";
    public const string OidcRevoke = "/connect/revoke";
    public const string OidcLogout = "/connect/logout";
    public const string OidcJwks = "/connect/jwks";
    public const string OidcRegister = "/connect/register";
    public const string Admin = ApiV1 + "/admin";
    public const string AdminIam = Admin + "/iam";
    /// <summary>
    /// Identity Workbench resource vocabulary. The public base route remains
    /// <c>/admin/iam</c> for backwards compatibility; consumers must use these
    /// names instead of composing ad-hoc route strings.
    /// </summary>
    public static class IdentityWorkbench
    {
        public const string Base = AdminIam;
        public const string Overview = Base + "/overview";
        public const string Scopes = Base + "/scopes";
        public const string Users = Base + "/users";
        public const string ExternalIdentities = Base + "/external-identities";
        public const string ServicePrincipals = Base + "/service-principals";
        public const string Clients = Base + "/clients";
        public const string Services = Base + "/services";
        public const string PermissionSets = Base + "/permission-sets";
        public const string Assignments = Base + "/assignments";
        public const string WorkloadRoles = Base + "/workload-roles";
        public const string Groups = Base + "/groups";
        public const string Boundaries = Base + "/boundaries";
        public const string ResourcePolicies = Base + "/resource-policies";
        public const string ApiAudiences = Base + "/api-audiences";
        public const string TrustedIssuers = Base + "/trusted-issuers";
        public const string Policies = Base + "/policies";
        public const string AccessRequests = Base + "/access-requests";
        public const string AccessReviews = Base + "/access-reviews";
        public const string BreakGlassRequests = Base + "/break-glass/requests";
        public const string AuthorizationChanges = Base + "/authorization-changes";
        public const string AuthorizationChangeRequests = Base + "/authorization-change-requests";
        public const string Sessions = Base + "/sessions";
        public const string WorkloadSessions = Base + "/workload-sessions";
        public const string Revocations = Base + "/revocations";
        public const string AuditLogs = Base + "/audit-logs";
        public static string UserSessions(Guid userId) => $"{Base}/users/{userId:D}/sessions";
        public static string UserSession(Guid userId, string sessionId) => $"{UserSessions(userId)}/{Uri.EscapeDataString(sessionId)}";
        public static string RevokeAllUserSessions(Guid userId) => $"{UserSessions(userId)}/revoke-all";
        public static string ResetUserCredentials(Guid userId) => $"{Base}/users/{userId:D}/credentials/reset";
        public const string Analyzer = Base + "/analyzer";
        public const string EffectiveAccess = Analyzer + "/effective-access";
        public const string PolicySimulator = Analyzer + "/policy-simulator";
        public const string AccessDiff = Analyzer + "/access-diff";
        public const string UnusedPermissions = Analyzer + "/unused-permissions";
        public const string AuditIntegrations = Base + "/audit-integrations";
        public const string Actions = Base + "/actions";

        public static string Resource(string collection, Guid id) => $"{Base}/{collection}/{id:D}";
        public static string Action(string collection, Guid id, string action) => $"{Resource(collection, id)}/{action}";
    }
    public const string IamOverview = AdminIam + "/overview";
    public const string IamApiAudiences = AdminIam + "/api-audiences";
    public const string IamTrustedIssuers = AdminIam + "/trusted-issuers";
    public const string AdminPolicies = Admin + "/policies";
    public const string AdminAccessRequests = Admin + "/access-requests";
    public const string AdminAccessReviews = Admin + "/access-reviews";
    public const string AdminAuthorizationChanges = Admin + "/authorization-changes";
    public const string AdminAuthorizationChangeRequests = Admin + "/authorization-change-requests";
    public const string AdminRebacListObjects = Admin + "/rebac/list-objects";
    public const string AdminBreakGlassRequests = Admin + "/break-glass/requests";
    public const string AdminPolicySimulate = Admin + "/policy/simulate";
    public const string AdminUsers = Admin + "/users";
    public const string AdminSessions = Admin + "/sessions";
    public const string AdminClients = Admin + "/clients";
    public const string AdminTables = Admin + "/tables";
    public const string AdminUsersBulk = Admin + "/users/bulk";
    public const string AdminUsersBulkCsv = AdminUsersBulk + "/csv";
    public const string AdminUsersBulkFile = AdminUsersBulk + "/file";
    public const string AdminUsersBulkPreview = AdminUsersBulk + "/preview";
    public const string AdminProvisioning = Admin + "/provisioning";
    public const string AdminProvisioningReadiness = AdminProvisioning + "/readiness";
    public const string AdminProvisioningDeliveryHealth = AdminProvisioning + "/delivery-health";
    public const string AdminProvisioningQueue = AdminProvisioning + "/queue";
    public const string AdminProvisioningJobs = AdminProvisioning + "/jobs";
    public const string AdminProvisioningReconcile = AdminProvisioning + "/reconcile";
    public const string AdminProvisioningReconcileScim = AdminProvisioning + "/reconcile/scim";
    public const string AdminMobile = Admin + "/mobile";
    public const string AdminPush = Admin + "/push";
    public const string AdminPushDeliverySummary = AdminPush + "/delivery-summary";
    public const string AdminPushNotifications = AdminPush + "/notifications";
    public const string AdminSecuritySignals = Admin + "/security-signals";
    public const string AdminSecuritySignalsStatus = AdminSecuritySignals + "/status";
    public const string AdminSecuritySignalsOutbox = AdminSecuritySignals + "/outbox";
    public const string Passkeys = Auth + "/passkeys";
    public const string PasskeyStatus = Passkeys + "/status";
    public const string PasskeyRegisterOptions = Passkeys + "/register/options";
    public const string PasskeyRegisterComplete = Passkeys + "/register/complete";
    public const string PasskeyAuthenticateOptions = Passkeys + "/authenticate/options";
    public const string PasskeyAuthenticateComplete = Passkeys + "/authenticate/complete";
    public const string PasskeyMfaOptions = Passkeys + "/mfa/options";
    public const string PasskeyMfaComplete = Passkeys + "/mfa/complete";
    public const string NativeMfaStart = Passkeys + "/mfa/native/start";
    public const string NativeMfaPoll = Passkeys + "/mfa/native/poll";
    public const string NativeMfaOptions = Passkeys + "/mfa/native/options";
    public const string NativeMfaComplete = Passkeys + "/mfa/native/complete";
    public const string NativeMfaReject = Passkeys + "/mfa/native/reject";
    public const string Mobile = ApiV1 + "/mobile";
    public const string MobileAppPolicy = Mobile + "/app-policy";
    public const string MobilePushTokens = Mobile + "/push-tokens";
    public const string AuditEvents = ApiV1 + "/audit/events";
    public const string FederationSamlLogin = ApiV1 + "/federation/saml/login";
    public const string MtlsLogin = Auth + "/mtls/login";
    public const string AdminMtls = Admin + "/mtls";
    public const string AdminMtlsBindings = AdminMtls + "/bindings";
    public const string RadiusEapTls = Auth + "/radius/eap-tls";
    public const string AdminRadiusEapTlsStatus = Admin + "/radius/eap-tls/status";
    public const string DevicePosture = ApiV1 + "/device-posture";
    public const string DevicePostureDecision = DevicePosture + "/decision";
    public const string AdminDevicePosture = Admin + "/device-posture";
    public const string AdminDevicePosturePolicy = AdminDevicePosture + "/policy";
    public const string AdminDevicePostureAssessments = AdminDevicePosture + "/assessments";
    public const string AdminDevicePosturePreview = AdminDevicePosture + "/preview";

    public const string UsersSegment = "/users";
    public const string RolesSegment = "/roles";
    public const string PermissionsSegment = "/permissions";
    public const string SettingsSegment = "/settings";
    public const string MfaEnrollSegment = "/mfa/enroll";
    public const string MfaMethodsSegment = "/mfa/methods";
    public const string MfaVerifySegment = "/mfa/verify";
    public const string MfaRecoverSegment = "/mfa/recover";
    public const string PasskeyStatusSegment = "/status";
    public const string PasskeyRegisterOptionsSegment = "/register/options";
    public const string PasskeyRegisterCompleteSegment = "/register/complete";
    public const string PasskeyAuthenticateOptionsSegment = "/authenticate/options";
    public const string PasskeyAuthenticateCompleteSegment = "/authenticate/complete";
    public const string PasskeyMfaOptionsSegment = "/mfa/options";
    public const string PasskeyMfaCompleteSegment = "/mfa/complete";
    public const string NativeMfaStartSegment = "/mfa/native/start";
    public const string NativeMfaPollSegment = "/mfa/native/poll";
    public const string NativeMfaOptionsSegment = "/mfa/native/options";
    public const string NativeMfaRejectSegment = "/mfa/native/reject";
    public const string SessionExchangeSegment = "/session/exchange";

    public static string User(Guid id) => $"{Users}/{id:D}";
    public static string Role(Guid id) => $"{Roles}/{id:D}";
    public static string Setting(string key) => $"{Settings}/{Uri.EscapeDataString(key)}";
    public static string ScimUsers => $"{ScimV2}/Users";
    public static string IamScope(Guid id) => IdentityWorkbench.Resource("scopes", id);
    public static string IamPermissionSet(Guid id) => IdentityWorkbench.Resource("permission-sets", id);
    public static string IamAssignment(Guid id) => IdentityWorkbench.Resource("assignments", id);
    public static string IamWorkloadRole(Guid id) => IdentityWorkbench.Resource("workload-roles", id);
    public static string IamWorkloadRoleSessions(Guid id) => $"{IamWorkloadRole(id)}/sessions";
    public static string IamWorkloadRoleSession(Guid id, string sessionId) => $"{IamWorkloadRoleSessions(id)}/{Uri.EscapeDataString(sessionId)}";
    public static string IamWorkloadRoleRevokeSessions(Guid id) => $"{IamWorkloadRole(id)}/revoke-sessions";
    public static string IamWorkloadRoleRotateCredential(Guid id) => $"{IamWorkloadRole(id)}/rotate-credential";
    public static string IamBoundary(Guid id) => IdentityWorkbench.Resource("boundaries", id);
    public static string DevicePostureDecisionFor(Guid userId, string deviceId) =>
        $"{DevicePostureDecision}/{userId:D}/{Uri.EscapeDataString(deviceId)}";
    public static string AdminUserSessions(Guid userId) => $"{AdminUsers}/{userId:D}/sessions";
    public static string AdminUserCredentialReset(Guid userId) => $"{AdminUsers}/{userId:D}/credentials/reset";
    public static string AdminSecuritySignalRetry(Guid id) => $"{AdminSecuritySignalsOutbox}/{id:D}/retry";
    public static string IamResourcePolicy(Guid id) => IdentityWorkbench.Resource("resource-policies", id);
    public static string IamGroup(Guid id) => IdentityWorkbench.Resource("groups", id);
    public static string AdminPolicy(Guid id) => $"{AdminPolicies}/{id:D}";
    public static string AdminPolicyCompile(Guid id) => $"{AdminPolicy(id)}/compile";
    public static string AdminAccessRequest(Guid id) => $"{AdminAccessRequests}/{id:D}";
    public static string AdminAccessReview(Guid id) => $"{AdminAccessReviews}/{id:D}";
    public static string AdminBreakGlassRequest(Guid id) => $"{AdminBreakGlassRequests}/{id:D}";
    public static string AdminEffectiveAccess(Guid id) => $"{AdminUsers}/{id:D}/effective-access";
    public static string AdminProvisioningJob(Guid id) => $"{AdminProvisioningJobs}/{id:D}";
    public static string AdminProvisioningJobRetry(Guid id) => $"{AdminProvisioningJob(id)}/retry";
}
