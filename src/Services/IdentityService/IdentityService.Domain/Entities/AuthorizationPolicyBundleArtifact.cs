namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// Immutable, hash-addressed artifact produced when the published policy set
/// is released. The database stores the signed artifact metadata so consumers
/// can audit exactly which policy bundle was delivered.
/// </summary>
public sealed class AuthorizationPolicyBundleArtifact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SchemaVersion { get; set; } = "authorization-policy-bundle.v1";
    public string Hash { get; set; } = string.Empty;
    public string PoliciesJson { get; set; } = "[]";
    public string Signature { get; set; } = string.Empty;
    public string? KeyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
