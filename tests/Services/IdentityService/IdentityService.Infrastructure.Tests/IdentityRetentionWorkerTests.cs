using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class IdentityRetentionWorkerTests
{
    [Fact]
    public async Task Worker_removes_records_older_than_each_retention_window_and_keeps_recent_records()
    {
        var services = new ServiceCollection();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureCreatedAsync();
            var old = DateTime.UtcNow.AddDays(-120);
            var userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                UserName = "retention-user",
                NormalizedUserName = "RETENTION-USER",
                Email = "retention@example.test",
                NormalizedEmail = "RETENTION@EXAMPLE.TEST",
                FirstName = "Retention",
                LastName = "User"
            });
            db.DirectoryProvisioningOutbox.AddRange(
                new DirectoryProvisioningOutbox { Id = Guid.NewGuid(), Target = "ldap", Operation = "update", CompletedAt = old },
                new DirectoryProvisioningOutbox { Id = Guid.NewGuid(), Target = "ldap", Operation = "update", CompletedAt = DateTime.UtcNow });
            db.SecuritySignalOutbox.AddRange(
                new SecuritySignalOutbox { Id = Guid.NewGuid(), EventType = "logout", Subject = "u1", DispatchedAt = old },
                new SecuritySignalOutbox { Id = Guid.NewGuid(), EventType = "logout", Subject = "u2", DispatchedAt = DateTime.UtcNow });
            db.PushNotificationOutbox.AddRange(
                new PushNotificationOutbox { Id = Guid.NewGuid(), UserId = "u1", ProcessedAt = old },
                new PushNotificationOutbox { Id = Guid.NewGuid(), UserId = "u2", ProcessedAt = DateTime.UtcNow });
            db.MobileTelemetryEvents.AddRange(
                new MobileTelemetryEvent { Id = Guid.NewGuid(), EventType = "error", Name = "old", CreatedAt = old },
                new MobileTelemetryEvent { Id = Guid.NewGuid(), EventType = "error", Name = "new", CreatedAt = DateTime.UtcNow });
            db.SecurityEvents.AddRange(
                new SecurityEvent { Id = Guid.NewGuid(), EventType = "login_failed", Timestamp = old },
                new SecurityEvent { Id = Guid.NewGuid(), EventType = "login_success", Timestamp = DateTime.UtcNow });
            db.DevicePostureAssessments.AddRange(
                new DevicePostureAssessment { Id = Guid.NewGuid(), UserId = userId, DeviceId = "old", Provider = "test", EvidenceHash = "old", CreatedAt = old },
                new DevicePostureAssessment { Id = Guid.NewGuid(), UserId = userId, DeviceId = "new", Provider = "test", EvidenceHash = "new", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var worker = new IdentityRetentionWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            CreateLock(),
            Options.Create(new IdentityRetentionOptions
            {
                CompletedOutboxDays = 90,
                ProcessedPushDays = 90,
                TelemetryDays = 90,
                SecurityEventDays = 90,
                DevicePostureDays = 90,
                BatchSize = 1,
                IntervalMinutes = 1,
                LockTtlMinutes = 1
            }),
            NullLogger<IdentityRetentionWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(async () =>
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            return await db.DirectoryProvisioningOutbox.CountAsync() == 1
                && await db.SecuritySignalOutbox.CountAsync() == 1
                && await db.PushNotificationOutbox.CountAsync() == 1
                && await db.MobileTelemetryEvents.CountAsync() == 1
                && await db.SecurityEvents.CountAsync() == 1
                && await db.DevicePostureAssessments.CountAsync() == 1;
        });
        await StopAsync(worker);

        worker.Dispose();
        using var verifyScope = provider.CreateScope();
        var remaining = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal("u2", (await remaining.PushNotificationOutbox.SingleAsync()).UserId);
        Assert.Equal("new", (await remaining.DevicePostureAssessments.SingleAsync()).DeviceId);
    }

    [Fact]
    public async Task Worker_skips_cleanup_when_distributed_lock_is_held()
    {
        var services = new ServiceCollection();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.DirectoryProvisioningOutbox.Add(new DirectoryProvisioningOutbox
            {
                Target = "ldap", Operation = "update", CompletedAt = DateTime.UtcNow.AddDays(-100)
            });
            await db.SaveChangesAsync();
        }

        var database = new Mock<IDatabase>();
        database.Setup(x => x.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), When.NotExists)).ReturnsAsync(false);
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);
        var worker = new IdentityRetentionWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new IdentityRedisLock(redis.Object),
            Options.Create(new IdentityRetentionOptions { IntervalMinutes = 1 }),
            NullLogger<IdentityRetentionWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await StopAsync(worker);
        worker.Dispose();

        using var verifyScope = provider.CreateScope();
        Assert.Equal(1, await verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>().DirectoryProvisioningOutbox.CountAsync());
    }

    private static IdentityRedisLock CreateLock()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), When.NotExists)).ReturnsAsync(true);
        database.Setup(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), CommandFlags.None))
            .ReturnsAsync(default(RedisResult));
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);
        return new IdentityRedisLock(redis.Object);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (await predicate()) return;
            await Task.Delay(25);
        }

        throw new TimeoutException("Identity retention worker did not complete cleanup.");
    }

    private static async Task StopAsync(IdentityRetentionWorker worker)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(timeout.Token);
    }
}
