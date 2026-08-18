namespace His.Hope.Contracts;

public static class ApiProblemExtensions
{
    public const string CorrelationId = "correlationId";
    public const string ErrorCode = "errorCode";
}

public sealed record ApiProblemResponse(
    string Type,
    string Title,
    int Status,
    string? Detail = null,
    string? Instance = null,
    string? CorrelationId = null,
    string? ErrorCode = null,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    int? RetryAfterSeconds = null);

public static class ApiErrorCodes
{
    public const string Validation = "validation_error";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string UnprocessableEntity = "unprocessable_entity";
    public const string Conflict = "conflict";
    public const string RateLimited = "rate_limited";
    public const string Internal = "internal_error";
    public const string AuthenticationRejected = "authentication_rejected";
    public const string AuthenticationRequestInvalid = "authentication_request_invalid";
    public const string SessionCookieRequired = "session_cookie_required";
    public const string PermissionRequired = "permission_required";
    public const string UnsupportedExternalProvider = "unsupported_external_provider";
    public const string PasswordResetFieldsRequired = "password_reset_fields_required";
    public const string InvalidPasswordResetRequest = "invalid_password_reset_request";
    public const string PasswordResetRejected = "password_reset_rejected";
    public const string PasswordFieldsRequired = "password_fields_required";
    public const string PasswordMustChange = "password_must_change";
    public const string PasswordChangeRejected = "password_change_rejected";
    public const string EmailVerificationFieldsRequired = "email_verification_fields_required";
    public const string InvalidEmailVerificationRequest = "invalid_email_verification_request";
    public const string EmailVerificationRejected = "email_verification_rejected";
    public const string ClientRegistrationFieldsRequired = "client_registration_fields_required";
    public const string InvalidRedirectUris = "invalid_redirect_uris";
    public const string UnsupportedTokenEndpointAuthMethod = "unsupported_token_endpoint_auth_method";
    public const string FacilityScopeDenied = "facility_scope_denied";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string RoleRequestRejected = "role_request_rejected";
    public const string UserRequestRejected = "user_request_rejected";
    public const string EmptyPayload = "empty_payload";
    public const string InvalidJson = "invalid_json";
    public const string Timeout = "timeout";
    public const string InvalidAuthorizationRequest = "invalid_authorization_request";
    public const string UnknownClientApplication = "unknown_client_application";
    public const string InvalidClientRedirectUri = "invalid_client_redirect_uri";
    public const string MissingConsentRequest = "missing_consent_request";
    public const string ExpiredConsentRequest = "expired_consent_request";
    public const string InvalidConsentRequest = "invalid_consent_request";
    public const string UnsupportedLocale = "unsupported_locale";
    public const string LanguagePreferenceSaveFailed = "language_preference_save_failed";
    public const string RateLimitExceeded = "rate_limit_exceeded";
    public const string CannotUnlinkOnlyLogin = "cannot_unlink_only_login";
    public const string AccountUnlinkFailed = "account_unlink_failed";
    public const string UnsupportedProvider = "unsupported_provider";
    public const string CurrentSessionCannotBeRevoked = "current_session_cannot_be_revoked";
    public const string UnsupportedBulkAction = "unsupported_bulk_action";
    public const string UnsupportedExportFormat = "unsupported_export_format";
    public const string ProviderNotEnabled = "provider_not_enabled";
    public const string InvalidPushToken = "invalid_push_token";
    public const string UnsupportedMobilePlatform = "unsupported_mobile_platform";
    public const string InvalidCrashReport = "invalid_crash_report";
    public const string InvalidRumEvent = "invalid_rum_event";
    public const string InvalidSyncEnvelope = "invalid_sync_envelope";
    public const string UnsupportedSyncContract = "unsupported_sync_contract";
    public const string InvalidPushNotification = "invalid_push_notification";
    public const string PasskeyChallengeExpired = "passkey_challenge_expired";
    public const string InvalidPasskeyAttestation = "invalid_passkey_attestation";
    public const string InvalidViewResource = "invalid_view_resource";
    public const string InvalidViewIdentifier = "invalid_view_identifier";
    public const string InvalidAuditAction = "invalid_audit_action";
    public const string BulkUsersRequired = "bulk_users_required";
    public const string BulkUsersLimitExceeded = "bulk_users_limit_exceeded";
    public const string BulkImportNoValidRecords = "bulk_import_no_valid_records";
    public const string InvalidQuery = "invalid_query";
    public const string InvalidMfaState = "invalid_mfa_state";
    public const string InvalidTotpCode = "invalid_totp_code";
    public const string InvalidRecoveryCode = "invalid_recovery_code";
    public const string ScimFacilityTokenRequired = "scim_facility_token_required";
    public const string ScimFacilityScopeDenied = "scim_facility_scope_denied";
    public const string ScimUserAlreadyExists = "scim_user_already_exists";
    public const string ScimRoleAlreadyExists = "scim_role_already_exists";
    public const string UnsupportedAnalysisOperation = "unsupported_analysis_operation";
    public const string InvalidHrWebhookPayload = "invalid_hr_webhook_payload";
    public const string InvalidHrWebhookEvent = "invalid_hr_webhook_event";
    public const string HrWebhookEventMismatch = "hr_webhook_event_mismatch";
    public const string UnsupportedHrEventType = "unsupported_hr_event_type";
    public const string SystemRoleImmutable = "system_role_immutable";
    public const string RetiredRoleCannotBePublished = "retired_role_cannot_be_published";
    public const string PreviousRoleTemplateUnavailable = "previous_role_template_unavailable";
    public const string ReconciliationLimitExceeded = "reconciliation_limit_exceeded";
    public const string NotAuthenticated = "not_authenticated";
    public const string InvalidUserPermissionRequest = "invalid_user_permission_request";

    public static string ForStatus(int status) => status switch
    {
        400 => Validation,
        401 => Unauthorized,
        403 => Forbidden,
        404 => NotFound,
        422 => UnprocessableEntity,
        409 => Conflict,
        429 => RateLimited,
        >= 500 => Internal,
        _ => $"http_{status}"
    };
}

public sealed record ApiErrorLogEntry(
    string ErrorCode,
    int StatusCode,
    string Message,
    string Method,
    string Path,
    string CorrelationId,
    string TraceId,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public static class ApiConcurrencyHeaders
{
    public const string EntityTag = "ETag";
    public const string IfMatch = "If-Match";
    public const string IfNoneMatch = "If-None-Match";
}

public sealed record ConcurrencyConflict(string Resource, string ResourceId, string? ExpectedVersion, string? ActualVersion);
