namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// Time-bound vendor/HQ elevation into a customer tenant for cross-tenant mutations (ADR 017).
/// </summary>
public sealed class SupportElevation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OperatorUserId { get; set; }

    public string SourceTenant { get; set; } = string.Empty;

    public string TargetTenant { get; set; } = string.Empty;

    public string PermissionsJson { get; set; } = "[]";

    public string Status { get; set; } = "pending";

    public string? RequestedBy { get; set; }

    public string? ApprovedBy { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(1);
}
