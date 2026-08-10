using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Domain.Tests;

public class PermissionTests
{
    [Fact]
    public void Create_WithRequiredFields_ShouldSetProperties()
    {
        var permission = new Permission
        {
            Code = "patients.read",
            Name = "Read Patients",
            Group = "Patients"
        };

        permission.Code.Should().Be("patients.read");
        permission.Name.Should().Be("Read Patients");
        permission.Group.Should().Be("Patients");
        permission.Description.Should().BeNull();
        permission.IsSystem.Should().BeTrue();
        permission.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        permission.RolePermissions.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithAllProperties_ShouldSetCorrectly()
    {
        var permission = new Permission
        {
            Code = "appointments.write",
            Name = "Write Appointments",
            Group = "Appointments",
            Description = "Allows creating and modifying appointments",
            IsSystem = false
        };

        permission.Description.Should().Be("Allows creating and modifying appointments");
        permission.IsSystem.Should().BeFalse();
    }

    [Fact]
    public void AddRolePermission_ShouldAddToCollection()
    {
        var permission = new Permission
        {
            Code = "clinical.read",
            Name = "Read Clinical",
            Group = "Clinical"
        };
        var role = new Role { Name = "Doctor" };
        var rolePermission = new RolePermission
        {
            RoleId = role.Id,
            Role = role,
            PermissionCode = permission.Code,
            Permission = permission
        };

        permission.RolePermissions.Add(rolePermission);

        permission.RolePermissions.Should().HaveCount(1);
    }

    [Fact]
    public void Code_ShouldBeCaseSensitive()
    {
        var lower = new Permission { Code = "patients.read", Name = "Read", Group = "Patients" };
        var upper = new Permission { Code = "Patients.Read", Name = "Read", Group = "Patients" };

        lower.Code.Should().NotBe(upper.Code);
    }

    [Theory]
    [InlineData("patients.read")]
    [InlineData("patients.write")]
    [InlineData("patients.delete")]
    [InlineData("appointments.read")]
    [InlineData("clinical.read")]
    [InlineData("admin.users")]
    public void WithVariousPermissionCodes_ShouldStoreCorrectly(string code)
    {
        var permission = new Permission
        {
            Code = code,
            Name = "Test",
            Group = "Test"
        };

        permission.Code.Should().Be(code);
    }

    [Fact]
    public void IsSystem_DefaultsToTrue()
    {
        var permission = new Permission
        {
            Code = "test.code",
            Name = "Test",
            Group = "Test"
        };

        permission.IsSystem.Should().BeTrue();
    }

    [Fact]
    public void CreatedAt_DefaultsToUtcNow()
    {
        var permission = new Permission
        {
            Code = "test.code",
            Name = "Test",
            Group = "Test"
        };

        permission.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
