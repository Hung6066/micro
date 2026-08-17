using His.Hope.IdentityService.Domain.Entities;
using His.Hope.SharedKernel.Authorization;

namespace His.Hope.IdentityService.Testing;

/// <summary>
/// Deterministic, side-effect-free builders for Identity tests.
/// Builders return fresh entities; persistence and isolation remain the
/// responsibility of each test fixture.
/// </summary>
public static class IdentityTestData
{
    public const string DefaultPassword = "Test@123456";
    public const string AdminEmail = "admin@hishop.com";
    public const string AdminUserName = "admin";
    public static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static User Admin() => new()
    {
        Id = AdminId,
        UserName = AdminUserName,
        NormalizedUserName = AdminUserName.ToUpperInvariant(),
        Email = AdminEmail,
        NormalizedEmail = AdminEmail.ToUpperInvariant(),
        FirstName = "Test",
        LastName = "Admin",
        IsActive = true,
        EmailConfirmed = true,
        CreatedAt = DateTime.UtcNow,
    };

    public static User User(
        string userName = "test-user",
        string email = "test-user@example.test",
        bool isActive = true,
        string firstName = "Test",
        string lastName = "User") => new()
    {
        UserName = userName,
        NormalizedUserName = userName.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        EmailConfirmed = true,
        FirstName = firstName,
        LastName = lastName,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow
    };

    public static Role Role(
        string name = "TestRole",
        string? description = "Role used by an isolated test") => new()
    {
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        Description = description,
        IsSystem = false,
        CreatedAt = DateTime.UtcNow
    };

    public static Permission Permission(
        string code = "test.read",
        string? name = "Test read",
        string group = "test") => new()
    {
        Code = code,
        Name = name ?? code,
        Group = group,
        Description = $"Permission {code}",
        IsSystem = false,
        CreatedAt = DateTime.UtcNow
    };

    public static IReadOnlyCollection<PermissionDescriptor> CanonicalPermissions() =>
        HisHopePermissions.AllDescriptors.ToArray();
}
