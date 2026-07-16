using Microsoft.AspNetCore.Identity;

namespace His.Hope.IdentityService.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Specialty { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public string FullName =>
        string.IsNullOrWhiteSpace(MiddleName)
            ? $"{LastName} {FirstName}"
            : $"{LastName} {MiddleName} {FirstName}";
}

public class Role : IdentityRole<Guid>
{
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether this is a system role that cannot be deleted.
    /// System roles include Admin, Provider, Nurse, etc.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// When the role was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
