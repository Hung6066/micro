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

    private sealed class TestableLdapBackgroundService(
        IServiceScopeFactory scopeFactory,
        Microsoft.Extensions.Logging.ILogger<LdapBackgroundService> logger)
        : LdapBackgroundService(scopeFactory, logger)
    {
        public Task RunAsync(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);
    }
}
