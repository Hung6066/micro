using Microsoft.AspNetCore.Identity;

namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// Join entity for the many-to-many relationship between User and Role.
/// Extends the ASP.NET Core Identity framework's IdentityUserRole with audit timestamps.
/// EF Core maps this to the AspNetUserRoles table with the additional AssignedAt column.
/// </summary>
public class UserRole : IdentityUserRole<Guid>
{
    /// <summary>
    /// When the role was assigned to the user.
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
