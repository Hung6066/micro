namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Singleton, versioned pilot policy. Observe is the safe default.</summary>
public sealed class DevicePosturePolicy
{
    public string ScopeId { get; set; } = IdentityScope.Global;
    public string Id { get; set; } = "default";
    public string Mode { get; set; } = "observe";
    public string ProvidersJson { get; set; } = "[\"chrome-enterprise\",\"advanced-compliance\",\"windows-local-login\"]";
    public int EvidenceTtlSeconds { get; set; } = 900;
    public string RequiredSignalsJson { get; set; } = "[]";
    public string Version { get; set; } = "1";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
