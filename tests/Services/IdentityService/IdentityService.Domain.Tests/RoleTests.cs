using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Domain.Tests;

public class RoleTests
{
    [Fact]
    public void Create_WithNameOnly_ShouldSetProperties()
    {
        var role = new Role { Name = "Doctor" };

        role.Name.Should().Be("Doctor");
        role.Id.Should().NotBeEmpty();
        role.Description.Should().BeNull();
        role.IsSystem.Should().BeFalse();
        role.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        role.RolePermissions.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithAllProperties_ShouldSetCorrectly()
    {
        var role = new Role
        {
            Name = "Admin",
            Description = "System administrator",
            IsSystem = true
        };

        role.Name.Should().Be("Admin");
        role.Description.Should().Be("System administrator");
        role.IsSystem.Should().BeTrue();
    }

    [Fact]
    public void AddRolePermission_ShouldAddToCollection()
    {
        var role = new Role { Name = "Nurse" };
        var permission = new Permission
        {
            Code = "patients.read",
            Name = "Read Patients",
            Group = "Patients"
        };
        var rolePermission = new RolePermission
        {
            RoleId = role.Id,
            Role = role,
            PermissionCode = permission.Code,
            Permission = permission
        };

        role.RolePermissions.Add(rolePermission);

        role.RolePermissions.Should().HaveCount(1);
        role.RolePermissions.Should().Contain(rp => rp.PermissionCode == "patients.read");
    }

    [Fact]
    public void TwoRoles_WithSameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var role1 = new Role { Id = id, Name = "Doctor" };
        var role2 = new Role { Id = id, Name = "Doctor" };

        role1.Equals(role2).Should().BeTrue();
        (role1 == role2).Should().BeTrue();
    }

    [Fact]
    public void TwoRoles_WithDifferentIds_ShouldNotBeEqual()
    {
        var role1 = new Role { Name = "Doctor" };
        var role2 = new Role { Name = "Nurse" };

        role1.Equals(role2).Should().BeFalse();
        (role1 != role2).Should().BeTrue();
    }

    [Fact]
    public void CreatedAt_DefaultsToUtcNow()
    {
        var role = new Role { Name = "Receptionist" };
        role.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void IsSystem_Default_ShouldBeFalse()
    {
        var role = new Role { Name = "Temp" };
        role.IsSystem.Should().BeFalse();
    }
}
