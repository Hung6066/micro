using System.Reflection;
using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class LdapSyncServiceTests
{
    [Fact]
    public void Group_mapping_is_case_insensitive_and_deduplicated()
    {
        var method = typeof(LdapSyncService).GetMethod("MapGroupsToRoles", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var config = new LdapConfig
        {
            GroupRoleMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Doctors"] = "Provider",
                ["Nurses"] = "Nurse"
            }
        };

        var result = (List<string>)method!.Invoke(null, [new[] { "CN=doctors", "CN=DOCTORS", "CN=nurses" }, config])!;

        Assert.Equal(new[] { "Provider", "Nurse" }, result);
    }

    [Fact]
    public void Ldap_filter_escaping_blocks_wildcards_and_injection_delimiters()
    {
        var method = typeof(LdapSyncService).GetMethod("EscapeFilterValue", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var escaped = (string)method!.Invoke(null, ["a\\b*(c)\0"])!;

        Assert.Equal("a\\5cb\\2a\\28c\\29\\00", escaped);
    }

    [Fact]
    public void Disabled_default_ldap_configuration_fails_validation_closed()
    {
        var method = typeof(LdapSyncService).GetMethod("Validate", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var config = new LdapConfig { Enabled = true };

        var exception = Assert.Throws<TargetInvocationException>(() => method!.Invoke(null, [config]));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task Provision_new_user_maps_profile_and_roles()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var runtime = new ExternalIdentityProviderRuntime(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(), db);
        var manager = Manager();
        manager.Setup(x => x.FindByNameAsync("directory-user")).ReturnsAsync((User?)null);
        manager.Setup(x => x.FindByEmailAsync("directory@example.test")).ReturnsAsync((User?)null);
        manager.Setup(x => x.CreateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);
        manager.Setup(x => x.GetRolesAsync(It.IsAny<User>())).ReturnsAsync([]);
        manager.Setup(x => x.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var result = await new LdapSyncService(runtime, manager.Object, NullLogger<LdapSyncService>.Instance)
            .ProvisionUserAsync(new LdapUserProfile("directory-user", "directory@example.test", "Directory", "User", ["CN=Doctors"], true, "CN=directory-user"));

        result.UserName.Should().Be("directory-user");
        result.EmailConfirmed.Should().BeTrue();
        result.FirstName.Should().Be("Directory");
        manager.Verify(x => x.AddToRoleAsync(It.IsAny<User>(), "Provider"), Times.Once);
    }

    [Fact]
    public async Task Provision_existing_user_updates_and_removes_non_provider_roles()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var runtime = new ExternalIdentityProviderRuntime(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(), db);
        var existing = new User { UserName = "directory-user", Email = "old@example.test", IsActive = false };
        var manager = Manager();
        manager.Setup(x => x.FindByNameAsync("directory-user")).ReturnsAsync(existing);
        manager.Setup(x => x.GetRolesAsync(existing)).ReturnsAsync(["Nurse", "Provider"]);
        manager.Setup(x => x.UpdateAsync(existing)).ReturnsAsync(IdentityResult.Success);
        manager.Setup(x => x.RemoveFromRoleAsync(existing, "Nurse")).ReturnsAsync(IdentityResult.Success);

        await new LdapSyncService(runtime, manager.Object, NullLogger<LdapSyncService>.Instance)
            .ProvisionUserAsync(new LdapUserProfile("directory-user", "new@example.test", "Updated", "User", ["CN=Doctors"], true, "CN=directory-user"));

        existing.Email.Should().Be("new@example.test");
        existing.IsActive.Should().BeTrue();
        manager.Verify(x => x.RemoveFromRoleAsync(existing, "Nurse"), Times.Once);
        manager.Verify(x => x.RemoveFromRoleAsync(existing, "Provider"), Times.Never);
    }

    [Fact]
    public async Task Provision_by_email_reuses_existing_user_when_directory_name_is_new()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var runtime = Runtime(db);
        var existing = new User { UserName = "old-directory-name", Email = "directory@example.test" };
        var manager = Manager();
        manager.Setup(x => x.FindByNameAsync("new-directory-name")).ReturnsAsync((User?)null);
        manager.Setup(x => x.FindByEmailAsync("directory@example.test")).ReturnsAsync(existing);
        manager.Setup(x => x.UpdateAsync(existing)).ReturnsAsync(IdentityResult.Success);
        manager.Setup(x => x.GetRolesAsync(existing)).ReturnsAsync([]);
        manager.Setup(x => x.AddToRoleAsync(existing, "Provider")).ReturnsAsync(IdentityResult.Success);

        var result = await new LdapSyncService(runtime, manager.Object, NullLogger<LdapSyncService>.Instance)
            .ProvisionUserAsync(new LdapUserProfile(
                "new-directory-name", "directory@example.test", "Directory", "User", ["CN=Doctors"], true, "CN=user"));

        result.Should().BeSameAs(existing);
        existing.FirstName.Should().Be("Directory");
        manager.Verify(x => x.CreateAsync(It.IsAny<User>()), Times.Never);
        manager.Verify(x => x.UpdateAsync(existing), Times.Once);
    }

    [Fact]
    public async Task Provision_throws_when_identity_store_rejects_new_user()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var manager = Manager();
        manager.Setup(x => x.FindByNameAsync("directory-user")).ReturnsAsync((User?)null);
        manager.Setup(x => x.FindByEmailAsync("directory@example.test")).ReturnsAsync((User?)null);
        manager.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "duplicate" }));

        var act = () => new LdapSyncService(Runtime(db), manager.Object, NullLogger<LdapSyncService>.Instance)
            .ProvisionUserAsync(new LdapUserProfile(
                "directory-user", "directory@example.test", null, null, null, true, "CN=user"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unable to provision LDAP user.");
        manager.Verify(x => x.GetRolesAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Sync_disabled_configuration_exits_without_connecting()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var runtime = new ExternalIdentityProviderRuntime(
            new ConfigurationBuilder().AddInMemoryCollection().Build(), db);
        var manager = Manager();

        await new LdapSyncService(runtime, manager.Object, NullLogger<LdapSyncService>.Instance).SyncAsync();

        manager.Verify(x => x.CreateAsync(It.IsAny<User>()), Times.Never);
        manager.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Sync_enabled_configuration_contains_connection_errors_without_throwing()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ldap:Enabled"] = "true",
            ["Ldap:Server"] = "127.0.0.1",
            ["Ldap:Port"] = "1",
            ["Ldap:BindDn"] = "cn=service",
            ["Ldap:BindPassword"] = "secret",
            ["Ldap:SearchBase"] = "dc=example,dc=test",
            ["Ldap:UseSsl"] = "true"
        }).Build();
        var manager = Manager();

        var act = () => new LdapSyncService(
            new ExternalIdentityProviderRuntime(configuration, db),
            manager.Object,
            NullLogger<LdapSyncService>.Instance).SyncAsync();

        await act.Should().NotThrowAsync();
        manager.Verify(x => x.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateMissingUsers_deactivates_confirmed_users_not_seen_in_directory()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var missing = new User { UserName = "missing", EmailConfirmed = true, IsActive = true };
        var present = new User { UserName = "present", EmailConfirmed = true, IsActive = true };
        var unconfirmed = new User { UserName = "unconfirmed", EmailConfirmed = false, IsActive = true };
        db.Users.AddRange(missing, present, unconfirmed);
        await db.SaveChangesAsync();

        var manager = Manager();
        manager.SetupGet(x => x.Users).Returns(db.Users);
        manager.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);
        var service = new LdapSyncService(Runtime(db), manager.Object, NullLogger<LdapSyncService>.Instance);
        var method = typeof(LdapSyncService).GetMethod("DeactivateMissingUsers", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        var task = (Task)method!.Invoke(service, [new HashSet<string>(["present"]), CancellationToken.None])!;
        await task;

        missing.IsActive.Should().BeFalse();
        present.IsActive.Should().BeTrue();
        unconfirmed.IsActive.Should().BeTrue();
        manager.Verify(x => x.UpdateAsync(missing), Times.Once);
        manager.Verify(x => x.UpdateAsync(It.Is<User>(u => u.UserName != "missing")), Times.Never);
    }

    [Fact]
    public async Task Provision_honors_cancellation_before_reading_or_mutating_users()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var manager = Manager();

        var act = () => new LdapSyncService(Runtime(db), manager.Object, NullLogger<LdapSyncService>.Instance)
            .ProvisionUserAsync(new LdapUserProfile("directory-user", "directory@example.test", null, null, null, true, "CN=user"), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        manager.Verify(x => x.FindByNameAsync(It.IsAny<string>()), Times.Never);
        manager.Verify(x => x.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Authenticate_disabled_or_blank_credentials_fail_closed()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var runtime = new ExternalIdentityProviderRuntime(
            new ConfigurationBuilder().AddInMemoryCollection().Build(), db);
        var manager = Manager();
        var service = new LdapSyncService(runtime, manager.Object, NullLogger<LdapSyncService>.Instance);

        (await service.AuthenticateAsync(string.Empty, "password")).Should().BeFalse();
        (await service.AuthenticateAndGetProfileAsync("user", string.Empty)).Should().BeNull();
    }

    private static Mock<UserManager<User>> Manager() => new(
        new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    private static ExternalIdentityProviderRuntime Runtime(IdentityDbContext db) =>
        new(new ConfigurationBuilder().AddInMemoryCollection().Build(), db);
}
