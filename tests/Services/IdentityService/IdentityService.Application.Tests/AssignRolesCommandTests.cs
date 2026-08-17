using FluentAssertions;
using His.Hope.IdentityService.Application.UseCases.Users.Commands;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class AssignRolesCommandTests
{
    [Fact]
    public async Task Unknown_user_is_rejected_before_role_mutation()
    {
        await using var db = TestApplicationDbContext.Create();
        var (users, roles) = Managers(db);
        var handler = new AssignRolesCommandHandler(users.Object, roles.Object);

        var act = () => handler.Handle(
            new AssignRolesCommand(Guid.NewGuid(), ["Clinician"]),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        users.Verify(x => x.AddToRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task Role_ids_are_resolved_and_existing_roles_are_replaced()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User
        {
            Id = Guid.NewGuid(), UserName = "clinician", Email = "clinician@example.test",
            FirstName = "An", LastName = "Nguyen", CreatedAt = DateTime.UtcNow
        };
        var role = new Role { Id = Guid.NewGuid(), Name = "Clinician" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var (users, roles) = Managers(db);
        users.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["OldRole"]);
        users.Setup(x => x.RemoveFromRolesAsync(user, It.Is<IEnumerable<string>>(r => r.Single() == "OldRole")))
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(x => x.AddToRolesAsync(user, It.Is<IEnumerable<string>>(r => r.Single() == "Clinician")))
            .ReturnsAsync(IdentityResult.Success);
        users.SetupSequence(x => x.GetRolesAsync(user)).ReturnsAsync(["OldRole"]).ReturnsAsync(["Clinician"]);
        roles.Setup(x => x.FindByIdAsync(role.Id.ToString())).ReturnsAsync(role);
        var handler = new AssignRolesCommandHandler(users.Object, roles.Object);

        var result = await handler.Handle(
            new AssignRolesCommand(user.Id, [role.Id.ToString()]),
            CancellationToken.None);

        result.UserName.Should().Be("clinician");
        result.Roles.Should().Equal("Clinician");
        users.Verify(x => x.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()), Times.Once);
        users.Verify(x => x.AddToRolesAsync(user, It.IsAny<IEnumerable<string>>()), Times.Once);
    }

    [Fact]
    public async Task Separation_of_duties_conflict_fails_closed_before_mutation()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "admin", Email = "admin@example.test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var (users, roles) = Managers(db);
        roles.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((Role?)null);
        var handler = new AssignRolesCommandHandler(users.Object, roles.Object);

        var act = () => handler.Handle(
            new AssignRolesCommand(user.Id, ["Provider", "BillingClerk"]),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ROLE_SOD_CONFLICT:*");
        users.Verify(x => x.RemoveFromRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        users.Verify(x => x.AddToRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    private static (Mock<UserManager<User>> Users, Mock<RoleManager<Role>> Roles) Managers(TestApplicationDbContext db)
    {
        var userStore = new Mock<IUserStore<User>>();
        var roleStore = new Mock<IRoleStore<Role>>();
        var users = new Mock<UserManager<User>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var roles = new Mock<RoleManager<Role>>(
            roleStore.Object, null!, null!, null!, null!);
        users.SetupGet(x => x.Users).Returns(db.Users);
        return (users, roles);
    }
}
