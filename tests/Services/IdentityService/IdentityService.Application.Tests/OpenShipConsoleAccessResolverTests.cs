using FluentAssertions;
using His.Hope.IdentityService.Application.OpenIddict;
using His.Hope.IdentityService.Application.OpenShip;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Abstractions;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class OpenShipConsoleAccessResolverTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Write_until_is_the_latest_active_approved_console_write_break_glass()
    {
        await using var db = TestApplicationDbContext.Create();
        var userId = Guid.NewGuid();
        db.BreakGlassRequests.AddRange(
            Request(userId, "approved", Now.AddMinutes(10)),
            Request(userId, "approved", Now.AddMinutes(25)),
            // Ignored: wrong permission, pending, revoked, expired, other subject.
            Request(userId, "approved", Now.AddMinutes(29), permission: "admin.users.read"),
            Request(userId, "pending", Now.AddMinutes(28)),
            Request(userId, "approved", Now.AddMinutes(27), revokedAt: Now.AddMinutes(-1)),
            Request(userId, "approved", Now.AddMinutes(-1)),
            Request(Guid.NewGuid(), "approved", Now.AddMinutes(30)));
        await db.SaveChangesAsync();

        var access = await Resolver(db).ResolveAsync(userId, CancellationToken.None);

        access.WriteUntil.Should().Be(Now.AddMinutes(25));
    }

    [Fact]
    public async Task Write_until_is_null_without_an_active_approved_request()
    {
        await using var db = TestApplicationDbContext.Create();
        var userId = Guid.NewGuid();
        db.BreakGlassRequests.AddRange(
            Request(userId, "pending", Now.AddMinutes(10)),
            Request(userId, "approved", Now));
        await db.SaveChangesAsync();

        var access = await Resolver(db).ResolveAsync(userId, CancellationToken.None);

        access.WriteUntil.Should().BeNull();
        access.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task Groups_are_active_memberships_with_the_configured_prefix_only()
    {
        await using var db = TestApplicationDbContext.Create();
        var userId = Guid.NewGuid();
        var groups = new[]
        {
            new IamGroup { Key = "openship-console-read" },
            new IamGroup { Key = "openship-console-admin" },
            new IamGroup { Key = "openship-console-old", IsActive = false },
            new IamGroup { Key = "Openship-console-case" },
            new IamGroup { Key = "billing" }
        };
        db.IamGroups.AddRange(groups);
        db.IamGroupMemberships.AddRange(groups.Select(group => new IamGroupMembership { GroupId = group.Id, UserId = userId }));
        var unrelated = new IamGroup { Key = "openship-console-other-user" };
        db.IamGroups.Add(unrelated);
        db.IamGroupMemberships.Add(new IamGroupMembership { GroupId = unrelated.Id, UserId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var access = await Resolver(db).ResolveAsync(userId, CancellationToken.None);

        access.Groups.Should().Equal("openship-console-admin", "openship-console-read");
    }

    [Fact]
    public void Options_defaults_are_applied_when_unconfigured()
    {
        var options = new OpenShipConsoleOptions { Clients = [" ", "openship-grafana", "openship-grafana"] };
        options.ApplyDefaults();
        options.Clients.Should().Equal("openship-grafana");

        var empty = new OpenShipConsoleOptions();
        empty.ApplyDefaults();
        empty.Clients.Should().Equal("openship-headscale", "openship-grafana", "openship-vault", "openship-headplane");
        empty.GroupPrefix.Should().Be("openship-console-");
        empty.WritePermission.Should().Be("openship.console.write");
        empty.IsConsoleClient("openship-vault").Should().BeTrue();
        empty.IsConsoleClient("OPENSHIP-VAULT").Should().BeFalse();
        empty.IsConsoleClient(null).Should().BeFalse();
    }

    [Fact]
    public async Task Subject_revoker_revokes_every_authorization_and_token_of_the_subject()
    {
        var subject = Guid.NewGuid();
        var authorizations = new Mock<IOpenIddictAuthorizationManager>();
        var tokens = new Mock<IOpenIddictTokenManager>();
        object[] authorizationEntries = [new object(), new object()];
        object[] tokenEntries = [new object(), new object(), new object()];
        authorizations.Setup(x => x.FindBySubjectAsync(subject.ToString(), It.IsAny<CancellationToken>()))
            .Returns(authorizationEntries.ToAsyncEnumerable());
        tokens.Setup(x => x.FindBySubjectAsync(subject.ToString(), It.IsAny<CancellationToken>()))
            .Returns(tokenEntries.ToAsyncEnumerable());
        authorizations.Setup(x => x.TryRevokeAsync(It.IsAny<object>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        // An already-revoked token reports false and is not counted.
        tokens.SetupSequence(x => x.TryRevokeAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true).ReturnsAsync(false).ReturnsAsync(true);

        var revoked = await new OpenIddictSubjectRevoker(authorizations.Object, tokens.Object, NullLogger<OpenIddictSubjectRevoker>.Instance)
            .RevokeAllAsync(subject, CancellationToken.None);

        revoked.Should().Be(4);
        foreach (var entry in authorizationEntries)
            authorizations.Verify(x => x.TryRevokeAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        foreach (var entry in tokenEntries)
            tokens.Verify(x => x.TryRevokeAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static OpenShipConsoleAccessResolver Resolver(TestApplicationDbContext db)
    {
        var options = new OpenShipConsoleOptions();
        options.ApplyDefaults();
        return new OpenShipConsoleAccessResolver(db, Options.Create(options), new FixedTimeProvider(Now));
    }

    private static BreakGlassRequest Request(Guid subject, string status, DateTime expiresAt,
        string permission = "openship.console.write", DateTime? revokedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        SubjectUserId = subject,
        PermissionCode = permission,
        FacilityId = "facility-a",
        Reason = "Console write for incident response",
        Status = status,
        RequestedBy = Guid.NewGuid().ToString(),
        RequestedAt = Now.AddMinutes(-5),
        ExpiresAt = expiresAt,
        RevokedAt = revokedAt
    };

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}

internal static class AsyncEnumerableTestExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
