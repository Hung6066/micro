using FluentAssertions;
using His.Hope.IdentityService.Application.UseCases.Users.Commands;
using His.Hope.IdentityService.Application.UseCases.Users.Queries;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class UserLifecycleCommandHandlerTests
{
    [Fact]
    public async Task Activate_marks_existing_user_active_and_persists()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "inactive", Email = "inactive@example.test", IsActive = false };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var manager = CreateManager(db);
        var handler = new ActivateUserCommandHandler(manager.Object);

        await handler.Handle(new ActivateUserCommand(user.Id), CancellationToken.None);

        user.IsActive.Should().BeTrue();
        manager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Deactivate_marks_existing_user_inactive_and_persists()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "active", Email = "active@example.test", IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var manager = CreateManager(db);
        var handler = new DeactivateUserCommandHandler(manager.Object);

        await handler.Handle(new DeactivateUserCommand(user.Id), CancellationToken.None);

        user.IsActive.Should().BeFalse();
        manager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Lifecycle_commands_reject_unknown_user_without_update()
    {
        await using var db = TestApplicationDbContext.Create();
        var manager = CreateManager(db);

        await FluentActions.Invoking(() => new ActivateUserCommandHandler(manager.Object)
                .Handle(new ActivateUserCommand(Guid.NewGuid()), CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => new DeactivateUserCommandHandler(manager.Object)
                .Handle(new DeactivateUserCommand(Guid.NewGuid()), CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();
        manager.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Lifecycle_commands_surface_identity_update_errors()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "user", Email = "user@example.test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var manager = CreateManager(db);
        manager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "write failed" }));

        await FluentActions.Invoking(() => new ActivateUserCommandHandler(manager.Object)
                .Handle(new ActivateUserCommand(user.Id), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Failed to activate user: write failed");
        await FluentActions.Invoking(() => new DeactivateUserCommandHandler(manager.Object)
                .Handle(new DeactivateUserCommand(user.Id), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Failed to deactivate user: write failed");
    }

    [Fact]
    public async Task GetUserById_maps_roles_and_returns_null_for_unknown_user()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User
        {
            Id = Guid.NewGuid(), UserName = "clinician", Email = "clinician@example.test",
            FirstName = "An", LastName = "Nguyen", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var manager = CreateManager(db);
        manager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Clinician"]);

        var handler = new GetUserByIdQueryHandler(manager.Object);

        (await handler.Handle(new GetUserByIdQuery(Guid.NewGuid()), CancellationToken.None)).Should().BeNull();
        var result = await handler.Handle(new GetUserByIdQuery(user.Id), CancellationToken.None);
        result.Should().NotBeNull();
        result!.UserName.Should().Be("clinician");
        result.Roles.Should().ContainSingle().Which.Should().Be("Clinician");
    }

    private static Mock<UserManager<User>> CreateManager(TestApplicationDbContext db)
    {
        var manager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        manager.SetupGet(x => x.Users).Returns(db.Users);
        manager.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);
        return manager;
    }
}
