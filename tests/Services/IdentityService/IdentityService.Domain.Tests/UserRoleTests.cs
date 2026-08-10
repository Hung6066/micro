using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Domain.Tests;

public class UserRoleTests
{
    [Fact]
    public void Create_WithIds_ShouldSetProperties()
    {
        var userRole = new UserRole
        {
            UserId = Guid.NewGuid(),
            RoleId = Guid.NewGuid()
        };

        userRole.UserId.Should().NotBeEmpty();
        userRole.RoleId.Should().NotBeEmpty();
        userRole.AssignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNavigations_ShouldWireCorrectly()
    {
        var user = new User { Id = Guid.NewGuid(), UserName = "dr.smith" };
        var role = new Role { Name = "Doctor" };

        var userRole = new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            User = user,
            Role = role
        };

        userRole.User.Should().Be(user);
        userRole.Role.Should().Be(role);
        userRole.User.UserName.Should().Be("dr.smith");
        userRole.Role.Name.Should().Be("Doctor");
    }

    [Fact]
    public void AssignedAt_DefaultsToUtcNow()
    {
        var userRole = new UserRole
        {
            UserId = Guid.NewGuid(),
            RoleId = Guid.NewGuid()
        };

        userRole.AssignedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void SameUserAndRole_ShouldBeEqualByValue()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var ur1 = new UserRole { UserId = userId, RoleId = roleId };
        var ur2 = new UserRole { UserId = userId, RoleId = roleId };

        ur1.UserId.Should().Be(ur2.UserId);
        ur1.RoleId.Should().Be(ur2.RoleId);
    }

    [Fact]
    public void DifferentUserAndRole_ShouldNotMatch()
    {
        var ur1 = new UserRole { UserId = Guid.NewGuid(), RoleId = Guid.NewGuid() };
        var ur2 = new UserRole { UserId = Guid.NewGuid(), RoleId = Guid.NewGuid() };

        ur1.UserId.Should().NotBe(ur2.UserId);
        ur1.RoleId.Should().NotBe(ur2.RoleId);
    }

    [Fact]
    public void AssignedAt_CanBeSet_ShouldReflectValue()
    {
        var assignedAt = new DateTime(2024, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var userRole = new UserRole
        {
            UserId = Guid.NewGuid(),
            RoleId = Guid.NewGuid(),
            AssignedAt = assignedAt
        };

        userRole.AssignedAt.Should().Be(assignedAt);
    }
}
