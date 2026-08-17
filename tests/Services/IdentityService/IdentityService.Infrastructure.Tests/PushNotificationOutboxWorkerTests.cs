using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class PushNotificationOutboxWorkerTests
{
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();
    [Fact]
    public async Task Worker_marks_successful_delivery_processed_and_clears_lease()
    {
        await using var harness = CreateHarness(delivered: true);
        await harness.SeedAsync();
        await harness.Worker.StartAsync(CancellationToken.None);

        var item = await harness.WaitForAsync(item => item.ProcessedAt is not null);
        await harness.StopAsync();

        Assert.NotNull(item.ProcessedAt);
        Assert.Null(item.LeaseId);
        Assert.Null(item.LastError);
        Assert.Equal(1, harness.Delivery.Calls);
    }

    [Fact]
    public async Task Worker_backoffs_unaccepted_delivery_and_keeps_item_retryable()
    {
        await using var harness = CreateHarness(delivered: false);
        await harness.SeedAsync();
        await harness.Worker.StartAsync(CancellationToken.None);

        var item = await harness.WaitForAsync(item => item.LastError is not null);
        await harness.StopAsync();

        Assert.Null(item.ProcessedAt);
        Assert.Equal("No active device accepted the notification", item.LastError);
        Assert.True(item.AvailableAt > DateTime.UtcNow);
        Assert.Null(item.LeaseId);
    }

    private static Harness CreateHarness(bool delivered)
    {
        var services = new ServiceCollection();
        var databaseName = $"push-outbox-tests-{Guid.NewGuid():N}";
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, DatabaseRoot));
        var delivery = new FakeDeliveryService(delivered);
        services.AddSingleton<IPushDeliveryService>(delivery);
        var provider = services.BuildServiceProvider();
        var worker = new PushNotificationOutboxWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PushNotificationOutboxWorker>.Instance);
        return new Harness(provider, worker, delivery);
    }

    private sealed class Harness(ServiceProvider provider, PushNotificationOutboxWorker worker, FakeDeliveryService delivery) : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;
        public PushNotificationOutboxWorker Worker { get; } = worker;
        public FakeDeliveryService Delivery { get; } = delivery;

        public async Task SeedAsync()
        {
            using var scope = Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.PushNotificationOutbox.Add(new PushNotificationOutbox
            {
                UserId = "user-1",
                Title = "Test",
                Body = "Body",
                AvailableAt = DateTime.UtcNow.AddMinutes(-1)
            });
            await db.SaveChangesAsync();
        }

        public async Task<PushNotificationOutbox> WaitForAsync(Func<PushNotificationOutbox, bool> predicate)
        {
            for (var attempt = 0; attempt < 60; attempt++)
            {
                await Task.Delay(25);
                using var scope = Provider.CreateScope();
                var item = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().PushNotificationOutbox.AsNoTracking().SingleAsync();
                if (predicate(item)) return item;
            }

            throw new TimeoutException("Push outbox worker did not process the test item.");
        }

        public async Task StopAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Worker.StopAsync(timeout.Token);
        }

        public async ValueTask DisposeAsync()
        {
            Worker.Dispose();
            await Provider.DisposeAsync();
        }
    }

    private sealed class FakeDeliveryService(bool delivered) : IPushDeliveryService
    {
        public int Calls { get; private set; }
        public Task<Guid> EnqueueAsync(string userId, string title, string body, string? dataJson = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.NewGuid());

        public Task<bool> DeliverAsync(string userId, string title, string body, Guid? outboxId = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(delivered);
        }
    }
}
