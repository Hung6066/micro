using System.Threading.Channels;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Audit;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class DatabaseAuditBackgroundServiceTests
{
    [Fact]
    public async Task Background_service_persists_channel_entries_and_drains_on_shutdown()
    {
        var services = new ServiceCollection();
        var databaseName = $"audit-{Guid.NewGuid():N}";
        services.AddDbContext<IdentityDbContext>(options => options.UseInMemoryDatabase(databaseName));
        await using var provider = services.BuildServiceProvider();
        var channel = Channel.CreateUnbounded<PhiAuditEntry>();
        var service = new DatabaseAuditBackgroundService(
            channel,
            provider.GetRequiredService<IServiceScopeFactory>(),
            null,
            NullLogger<DatabaseAuditBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await channel.Writer.WriteAsync(new PhiAuditEntry
        {
            UserId = "user-1",
            Action = "READ",
            ResourceType = "Patient",
            ResourceId = "patient-1",
            HttpMethod = "GET",
            Path = "/api/patients/patient-1",
            Timestamp = DateTime.UtcNow
        });

        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(50);
            using var scope = provider.CreateScope();
            if (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AuditLogs.AnyAsync())
                break;
        }

        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StopAsync(stopTimeout.Token);
        using var verifyScope = provider.CreateScope();
        var audit = await verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>().AuditLogs.SingleAsync();
        Assert.Equal("READ", audit.Action);
        Assert.Equal("GET /api/patients/patient-1", audit.Details);
    }

    [Fact]
    public async Task ExecuteAsync_drains_entries_when_shutdown_is_already_requested()
    {
        var services = new ServiceCollection();
        var databaseName = $"audit-drain-{Guid.NewGuid():N}";
        services.AddDbContext<IdentityDbContext>(options => options.UseInMemoryDatabase(databaseName));
        await using var provider = services.BuildServiceProvider();
        var channel = Channel.CreateUnbounded<PhiAuditEntry>();
        await channel.Writer.WriteAsync(new PhiAuditEntry
        {
            UserId = "user-drain",
            Action = "WRITE",
            ResourceType = "User",
            ResourceId = "user-drain",
            HttpMethod = "POST",
            Path = "/api/users",
            Timestamp = DateTime.UtcNow
        });

        var service = new TestableDatabaseAuditBackgroundService(
            channel,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DatabaseAuditBackgroundService>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await service.RunAsync(cancellation.Token);

        using var scope = provider.CreateScope();
        var audit = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AuditLogs.SingleAsync();
        Assert.Equal("WRITE", audit.Action);
        Assert.Equal("POST /api/users", audit.Details);
    }

    private sealed class TestableDatabaseAuditBackgroundService(
        Channel<PhiAuditEntry> channel,
        IServiceScopeFactory scopeFactory,
        Microsoft.Extensions.Logging.ILogger<DatabaseAuditBackgroundService> logger)
        : DatabaseAuditBackgroundService(channel, scopeFactory, null, logger)
    {
        public Task RunAsync(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);
    }
}
