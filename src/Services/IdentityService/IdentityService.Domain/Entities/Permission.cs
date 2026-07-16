using System.ComponentModel.DataAnnotations;

namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// Represents a granular permission in the system.
/// Permissions are grouped by functional area (e.g., patients, appointments, clinical).
/// They are assigned to Roles via a many-to-many relationship.
/// </summary>
public class Permission
{
    /// <summary>
    /// Unique permission code (e.g., "patients.read", "patients.write").
    /// This is the system-wide identifier used for authorization checks.
    /// </summary>
    [Key]
    [MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name (e.g., "Đọc bệnh nhân").
    /// Displayed in role management UIs.
    /// </summary>
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Functional group for organizing permissions in UI (e.g., "Bệnh nhân", "Lịch hẹn").
    /// </summary>
    [MaxLength(100)]
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this permission grants.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this is a system-built permission that cannot be deleted.
    /// </summary>
    public bool IsSystem { get; set; } = true;

    /// <summary>
    /// When the permission was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>
/// Join entity for the many-to-many relationship between Role and Permission.
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public string PermissionCode { get; set; } = string.Empty;
    public Permission Permission { get; set; } = null!;
}
