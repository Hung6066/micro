using FluentAssertions;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class BulkUserImportServiceTests
{
    [Fact]
    public async Task Import_creates_user_assigns_existing_role_and_upserts_primary_facility()
    {
        await using var db = CreateDb();
        var users = UserManager();
        var roles = RoleManager();
        User? created = null;
        users.Setup(x => x.FindByNameAsync("new-user")).ReturnsAsync((User?)null);
        users.Setup(x => x.FindByEmailAsync("new@example.test")).ReturnsAsync((User?)null);
        users.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .Callback<User>(user => created = user)
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(x => x.AddToRoleAsync(It.IsAny<User>(), "Provider"))
            .ReturnsAsync(IdentityResult.Success);
        roles.Setup(x => x.RoleExistsAsync("Provider")).ReturnsAsync(true);

        var request = new BulkImportRequest([
            new BulkUserRecord("new-user", "new@example.test", "New", "User",
                MiddleName: "M", LicenseNumber: "LIC-1", Specialty: "Cardiology",
                Role: "Provider", FacilityId: "  FAC-1  ")], SkipExisting: true);

        var result = await Service(users, roles, db).ImportAsync(request);

        result.Should().BeEquivalentTo(new BulkImportResult(1, 1, 0, 0, []));
        created.Should().NotBeNull();
        created!.MiddleName.Should().Be("M");
        created.EmailConfirmed.Should().BeFalse();
        users.Verify(x => x.AddToRoleAsync(created, "Provider"), Times.Once);
        db.UserFacilities.Should().ContainSingle(x =>
            x.UserId == created.Id && x.FacilityId == "FAC-1" && x.IsPrimary && x.IsActive);
    }

    [Fact]
    public async Task Import_skips_existing_user_without_mutating_or_upserting_facility()
    {
        await using var db = CreateDb();
        var existing = new User { UserName = "existing", Email = "old@example.test" };
        var users = UserManager();
        var roles = RoleManager();
        users.Setup(x => x.FindByNameAsync("existing")).ReturnsAsync(existing);

        var request = new BulkImportRequest([
            new BulkUserRecord("existing", "new@example.test", "Changed", "Name", FacilityId: "FAC-2")]);

        var result = await Service(users, roles, db).ImportAsync(request);

        result.Succeeded.Should().Be(0);
        result.Skipped.Should().Be(1);
        existing.Email.Should().Be("old@example.test");
        users.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
        db.UserFacilities.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_updates_existing_user_and_reactivates_case_insensitive_facility()
    {
        await using var db = CreateDb();
        var existing = new User { UserName = "existing", Email = "old@example.test", IsActive = false };
        db.Users.Add(existing);
        db.UserFacilities.AddRange(
            new UserFacility { UserId = existing.Id, FacilityId = "FAC-1", IsPrimary = true, IsActive = true },
            new UserFacility { UserId = existing.Id, FacilityId = "FAC-2", IsPrimary = true, IsActive = true,
                RevokedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var users = UserManager();
        var roles = RoleManager();
        users.Setup(x => x.FindByNameAsync("existing")).ReturnsAsync(existing);
        users.Setup(x => x.UpdateAsync(existing)).ReturnsAsync(IdentityResult.Success);

        var result = await Service(users, roles, db).ImportAsync(new BulkImportRequest([
            new BulkUserRecord("existing", "updated@example.test", "Updated", "User", IsActive: true,
                FacilityId: " fac-2 ")], SkipExisting: false));

        result.Succeeded.Should().Be(1);
        existing.Email.Should().Be("updated@example.test");
        existing.FirstName.Should().Be("Updated");
        existing.IsActive.Should().BeTrue();
        var facilities = await db.UserFacilities.OrderBy(x => x.FacilityId).ToListAsync();
        facilities.Single(x => x.FacilityId == "FAC-1").IsPrimary.Should().BeFalse();
        var reactivated = facilities.Single(x => x.FacilityId == "FAC-2");
        reactivated.IsPrimary.Should().BeTrue();
        reactivated.IsActive.Should().BeTrue();
        reactivated.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Import_records_identity_creation_errors_and_continues_with_next_record()
    {
        await using var db = CreateDb();
        var users = UserManager();
        var roles = RoleManager();
        users.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        users.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        users.Setup(x => x.CreateAsync(It.Is<User>(u => u.UserName == "bad")))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Duplicate", Description = "already exists" }));
        users.Setup(x => x.CreateAsync(It.Is<User>(u => u.UserName == "good")))
            .ReturnsAsync(IdentityResult.Success);

        var result = await Service(users, roles, db).ImportAsync(new BulkImportRequest([
            new BulkUserRecord("bad", "bad@example.test", "Bad", "User"),
            new BulkUserRecord("good", "good@example.test", "Good", "User") ]));

        result.TotalSubmitted.Should().Be(2);
        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(new BulkImportError("bad", "already exists"));
    }

    [Fact]
    public async Task Import_catches_record_exception_and_returns_error_without_aborting_batch()
    {
        await using var db = CreateDb();
        var users = UserManager();
        var roles = RoleManager();
        users.Setup(x => x.FindByNameAsync("throws")).ThrowsAsync(new InvalidOperationException("directory unavailable"));
        users.Setup(x => x.FindByNameAsync("ok")).ReturnsAsync((User?)null);
        users.Setup(x => x.FindByEmailAsync("ok@example.test")).ReturnsAsync((User?)null);
        users.Setup(x => x.CreateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);

        var result = await Service(users, roles, db).ImportAsync(new BulkImportRequest([
            new BulkUserRecord("throws", "throws@example.test", "Throws", "User"),
            new BulkUserRecord("ok", "ok@example.test", "Okay", "User") ]));

        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Errors.Single().Error.Should().Be("directory unavailable");
    }

    [Fact]
    public async Task Import_does_not_assign_unknown_role_or_create_blank_facility()
    {
        await using var db = CreateDb();
        var users = UserManager();
        var roles = RoleManager();
        users.Setup(x => x.FindByNameAsync("user")).ReturnsAsync((User?)null);
        users.Setup(x => x.FindByEmailAsync("user@example.test")).ReturnsAsync((User?)null);
        users.Setup(x => x.CreateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);
        roles.Setup(x => x.RoleExistsAsync("Unknown")).ReturnsAsync(false);

        var result = await Service(users, roles, db).ImportAsync(new BulkImportRequest([
            new BulkUserRecord("user", "user@example.test", "First", "Last", Role: "Unknown", FacilityId: "  ") ]));

        result.Succeeded.Should().Be(1);
        users.Verify(x => x.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        db.UserFacilities.Should().BeEmpty();
    }

    private static BulkUserImportService Service(
        Mock<UserManager<User>> users,
        Mock<RoleManager<Role>> roles,
        IdentityDbContext db) =>
        new(users.Object, roles.Object, NullLogger<BulkUserImportService>.Instance, db);

    private static IdentityDbContext CreateDb() => new(
        new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"bulk-import-{Guid.NewGuid():N}")
            .Options);

    private static Mock<UserManager<User>> UserManager() => new(
        new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    private static Mock<RoleManager<Role>> RoleManager() => new(
        new Mock<IRoleStore<Role>>().Object, null!, null!, null!, null!);
}
