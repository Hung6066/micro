namespace His.Hope.IdentityService.Domain.Constants;

/// <summary>
/// Persisted workflow status values owned by IdentityService.
/// </summary>
public static class IdentityWorkflowStatuses
{
    public static class SupportElevation
    {
        public const string Pending = "pending";
        public const string Approved = "approved";
        public const string Revoked = "revoked";
    }
}
