namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// Immutable authorization template snapshot. The live Role row remains the
/// effective entitlement while this table provides an auditable version history
/// for publish and rollback operations.
/// </summary>
public sealed class RoleTemplateVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoleId { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Owner { get; set; } = "identity-service";
    public string RiskTier { get; set; } = "standard";
    public int ReviewCadenceDays { get; set; } = 180;
    public string LifecycleStatus { get; set; } = "published";
    public string PermissionsJson { get; set; } = "[]";
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }

    public Role Role { get; set; } = null!;
}
