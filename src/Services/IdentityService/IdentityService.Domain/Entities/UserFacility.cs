namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// Persisted facility membership used to scope authorization decisions.
/// Facility identifiers are owned by the deployment's facility registry.
/// </summary>
public class UserFacility
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string FacilityId { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}
