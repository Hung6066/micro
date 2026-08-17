using FluentAssertions;
using His.Hope.IdentityService.Application.UseCases.Users.Commands;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class UserCreateUpdateCommandTests
{
    [Fact]
    public async Task Create_user_uses_provider_default_and_maps_identity_result()
    {
        await using var db = TestApplicationDbContext.Create();
        var manager = Manager(db);
        manager.Setup(x => x.FindByNameAsync("new-user")).ReturnsAsync((User?)null);
        manager.Setup(x => x.FindByEmailAsync("new@example.test")).ReturnsAsync((User?)null);
        manager.Setup(x => x.CreateAsync(It.IsAny<User>(), "Password1!")).ReturnsAsync(IdentityResult.Success);
        manager.Setup(x => x.GetRolesAsync(It.IsAny<User>())).ReturnsAsync(["Provider"]);

        var result = await new CreateUserCommandHandler(manager.Object).Handle(
            new CreateUserCommand("new-user", "new@example.test", "Password1!", "An", "Nguyen", null, "LIC-1", "Cardiology", "+841", null),
            CancellationToken.None);

        result.UserName.Should().Be("new-user");
        result.FullName.Should().Be("Nguyen An");
        result.Roles.Should().ContainSingle("Provider");
        manager.Verify(x => x.AddToRoleAsync(It.IsAny<User>(), "Provider"), Times.Once);
    }

    [Fact]
    public async Task Create_user_rejects_duplicate_username_or_email_before_write()
    {
        await using var db = TestApplicationDbContext.Create();
        var manager = Manager(db);
        var existing = new User { UserName = "taken", Email = "taken@example.test" };
        manager.Setup(x => x.FindByNameAsync("taken")).ReturnsAsync(existing);

        await FluentActions.Invoking(() => new CreateUserCommandHandler(manager.Object).Handle(
                new CreateUserCommand("taken", "new@example.test", "Password1!", "A", "B", null, null, null, null, "Nurse"), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Username already exists.");

        manager.Reset();
        manager.Setup(x => x.FindByNameAsync("new-user")).ReturnsAsync((User?)null);
        manager.Setup(x => x.FindByEmailAsync("taken@example.test")).ReturnsAsync(existing);
        await FluentActions.Invoking(() => new CreateUserCommandHandler(manager.Object).Handle(
                new CreateUserCommand("new-user", "taken@example.test", "Password1!", "A", "B", null, null, null, null, "Nurse"), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Email already registered.");
        manager.Verify(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Create_user_surfaces_identity_errors()
    {
        await using var db = TestApplicationDbContext.Create();
        var manager = Manager(db);
        manager.Setup(x => x.FindByNameAsync("new-user")).ReturnsAsync((User?)null);
        manager.Setup(x => x.FindByEmailAsync("new@example.test")).ReturnsAsync((User?)null);
        manager.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "password rejected" }));

        await FluentActions.Invoking(() => new CreateUserCommandHandler(manager.Object).Handle(
                new CreateUserCommand("new-user", "new@example.test", "Password1!", "A", "B", null, null, null, null, "Nurse"), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("User creation failed: password rejected");
    }

    [Fact]
    public async Task Update_user_applies_fields_email_role_and_maps_result()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { UserName = "old", Email = "old@example.test", FirstName = "Old", LastName = "Name", ConcurrencyStamp = "v1" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var manager = Manager(db);
        manager.Setup(x => x.FindByEmailAsync("new@example.test")).ReturnsAsync((User?)null);
        manager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        manager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Nurse"]);

        var result = await new UpdateUserCommandHandler(manager.Object).Handle(
            new UpdateUserCommand(user.Id, "New", "Surname", "new@example.test", "+842", "Nurse", false, "v1"), CancellationToken.None);

        user.FirstName.Should().Be("New");
        user.Email.Should().Be("new@example.test");
        user.UserName.Should().Be("new@example.test");
        user.IsActive.Should().BeFalse();
        result.Roles.Should().ContainSingle("Nurse");
        manager.Verify(x => x.AddToRoleAsync(user, "Nurse"), Times.Once);
    }

    [Fact]
    public async Task Update_user_rejects_unknown_stale_and_duplicate_email()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { UserName = "user", Email = "user@example.test", ConcurrencyStamp = "v1" };
        var other = new User { UserName = "other", Email = "other@example.test" };
        db.Users.AddRange(user, other);
        await db.SaveChangesAsync();
        var manager = Manager(db);
        var handler = new UpdateUserCommandHandler(manager.Object);

        await FluentActions.Invoking(() => handler.Handle(new UpdateUserCommand(Guid.NewGuid(), null, null, null, null, null, null, null), CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => handler.Handle(new UpdateUserCommand(user.Id, null, null, null, null, null, null, "stale"), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("CONCURRENCY_CONFLICT:*");
        manager.Setup(x => x.FindByEmailAsync("other@example.test")).ReturnsAsync(other);
        await FluentActions.Invoking(() => handler.Handle(new UpdateUserCommand(user.Id, null, null, "other@example.test", null, null, null, "v1"), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Email already in use by another user.");
    }

    [Fact]
    public async Task Update_user_surfaces_identity_write_failure()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { UserName = "user", Email = "user@example.test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var manager = Manager(db);
        manager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "write failed" }));

        await FluentActions.Invoking(() => new UpdateUserCommandHandler(manager.Object).Handle(
                new UpdateUserCommand(user.Id, "Updated", null, null, null, null, null, null), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("User update failed: write failed");
    }

    private static Mock<UserManager<User>> Manager(TestApplicationDbContext db)
    {
        var manager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        manager.SetupGet(x => x.Users).Returns(db.Users);
        manager.Setup(x => x.NormalizeEmail(It.IsAny<string>())).Returns((string value) => value.ToUpperInvariant());
        manager.Setup(x => x.NormalizeName(It.IsAny<string>())).Returns((string value) => value.ToUpperInvariant());
        manager.Setup(x => x.GetRolesAsync(It.IsAny<User>())).ReturnsAsync([]);
        manager.Setup(x => x.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        manager.Setup(x => x.RemoveFromRolesAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        return manager;
    }
}
