using FluentAssertions;
using His.Hope.IdentityService.Application.UseCases.Roles.Commands;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Testing;
using Microsoft.AspNetCore.Identity;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class RoleCommandTests
{
    [Fact]
    public async Task CreateRole_validates_duplicates_and_permission_references()
    {
        await using var db = TestApplicationDbContext.Create();
        db.Permissions.Add(IdentityTestData.Permission("patients.read", "Read patients", "patients"));
        await db.SaveChangesAsync();
        var handler = new CreateRoleCommandHandler(db);

        var created = await handler.Handle(
            new CreateRoleCommand("Clinician", "Clinical access", [" patients.read "], null), CancellationToken.None);
        created.Name.Should().Be("Clinician");
        created.Permissions.Should().ContainSingle().Which.Code.Should().Be("patients.read");

        await FluentActions.Invoking(() => handler.Handle(
                new CreateRoleCommand("Clinician", null, null, null), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
        await FluentActions.Invoking(() => handler.Handle(
                new CreateRoleCommand("Billing", null, ["billing.void"], null), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Unknown permission*");
    }

    [Fact]
    public async Task UpdateRole_replaces_permissions_and_rejects_stale_or_system_roles()
    {
        await using var db = TestApplicationDbContext.Create();
        var role = IdentityTestData.Role("Clinician");
        var permission = IdentityTestData.Permission("patients.read");
        db.Roles.Add(role);
        db.Permissions.Add(permission);
        await db.SaveChangesAsync();
        var handler = new UpdateRoleCommandHandler(db);

        var updated = await handler.Handle(
            new UpdateRoleCommand(role.Id, "SeniorClinician", "Updated", [permission.Code], role.ConcurrencyStamp, "clinical"),
            CancellationToken.None);
        updated.Name.Should().Be("SeniorClinician");
        updated.Owner.Should().Be("clinical");

        await FluentActions.Invoking(() => handler.Handle(
                new UpdateRoleCommand(role.Id, "Other", null, null, "stale", null), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("CONCURRENCY_CONFLICT*");

        var system = IdentityTestData.Role("System");
        system.IsSystem = true;
        db.Roles.Add(system);
        await db.SaveChangesAsync();
        await FluentActions.Invoking(() => handler.Handle(
                new UpdateRoleCommand(system.Id, "System2", null, null, null, null), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*immutable*");
    }

    [Fact]
    public async Task DeleteRole_enforces_not_found_system_and_assignment_rules()
    {
        await using var db = TestApplicationDbContext.Create();
        var handler = new DeleteRoleCommandHandler(db);

        await FluentActions.Invoking(() => handler.Handle(new DeleteRoleCommand(Guid.NewGuid()), CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();

        var system = IdentityTestData.Role("System");
        system.IsSystem = true;
        var assigned = IdentityTestData.Role("Assigned");
        db.Roles.AddRange(system, assigned);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = Guid.NewGuid(), RoleId = assigned.Id });
        await db.SaveChangesAsync();

        await FluentActions.Invoking(() => handler.Handle(new DeleteRoleCommand(system.Id), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*System roles*");
        await FluentActions.Invoking(() => handler.Handle(new DeleteRoleCommand(assigned.Id), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*users assigned*");

        var removable = IdentityTestData.Role("Removable");
        db.Roles.Add(removable);
        await db.SaveChangesAsync();
        await handler.Handle(new DeleteRoleCommand(removable.Id), CancellationToken.None);
        (await db.Roles.FindAsync(removable.Id)).Should().BeNull();
    }
}
