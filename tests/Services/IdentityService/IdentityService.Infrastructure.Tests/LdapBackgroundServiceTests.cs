using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class LdapBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_when_cancelled_before_start_does_not_create_scope()
    {
        var scopes = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        var service = new TestableLdapBackgroundService(
            scopes.Object,
            NullLogger<LdapBackgroundService>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunAsync(cancellation.Token));

        scopes.Verify(factory => factory.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task ResolveSyncDelayAsync_uses_safe_fallback_when_database_configuration_cannot_be_read()
    {
        var delay = await LdapBackgroundService.ResolveSyncDelayAsync(
            _ => Task.FromException<int>(new InvalidOperationException("database unavailable")),
            NullLogger<LdapBackgroundService>.Instance,
            CancellationToken.None);

        Assert.Equal(TimeSpan.FromMinutes(5), delay);
    }

    private sealed class TestableLdapBackgroundService(
        IServiceScopeFactory scopeFactory,
        Microsoft.Extensions.Logging.ILogger<LdapBackgroundService> logger)
        : LdapBackgroundService(scopeFactory, logger)
    {
        public Task RunAsync(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);
    }
}
