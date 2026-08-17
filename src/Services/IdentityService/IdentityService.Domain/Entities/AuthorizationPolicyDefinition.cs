namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// Versioned, human-readable ABAC policy definition. Rules are JSON so the
/// control plane can evolve without putting policy decisions in the frontend.
/// Only the allow-listed rule keys are accepted by the application validator.
/// </summary>
public sealed class AuthorizationPolicyDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = "identity-service";
    public int Version { get; set; } = 1;
    public string LifecycleStatus { get; set; } = "draft";
    public string RulesJson { get; set; } = "{}";
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
}
