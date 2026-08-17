using System.Reflection;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class DirectoryProvisioningDispatcherTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public DirectoryProvisioningDispatcherTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DryRunCompletesOutboxWithoutExternalTargetCall()
    {
        var jobId = Guid.NewGuid();
        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.DirectoryProvisioningOutbox.Add(new DirectoryProvisioningOutbox
            {
                Id = jobId,
                Target = "entra",
                Operation = "create",
                ResourceType = "User",
                ResourceId = Guid.NewGuid().ToString(),
                PayloadJson = "{}",
                AvailableAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var dispatcher = new DirectoryProvisioningDispatcher(
            _fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PROVISIONING_MODE"] = "dry-run"
            }).Build(),
            NullLogger<DirectoryProvisioningDispatcher>.Instance);
        var dispatch = typeof(DirectoryProvisioningDispatcher).GetMethod("DispatchAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(dispatch);
        var task = (Task)dispatch!.Invoke(dispatcher, [CancellationToken.None])!;
        await task;

        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var entry = await db.DirectoryProvisioningOutbox.AsNoTracking().SingleAsync(item => item.Id == jobId);
            Assert.NotNull(entry.CompletedAt);
            Assert.Equal("dry_run_no_external_call", entry.LastError);
            Assert.Equal(1, entry.Attempts);
        }
    }

    [Fact]
    public async Task UnknownModeFailsClosedWithoutMutatingOutbox()
    {
        var jobId = Guid.NewGuid();
        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.DirectoryProvisioningOutbox.Add(new DirectoryProvisioningOutbox
            {
                Id = jobId,
                Target = "entra",
                Operation = "create",
                ResourceType = "User",
                ResourceId = Guid.NewGuid().ToString(),
                PayloadJson = "{}",
                AvailableAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var dispatcher = new DirectoryProvisioningDispatcher(
            _fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PROVISIONING_MODE"] = "enabeld"
            }).Build(),
            NullLogger<DirectoryProvisioningDispatcher>.Instance);
        var dispatch = typeof(DirectoryProvisioningDispatcher).GetMethod("DispatchAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(dispatch);
        await (Task)dispatch!.Invoke(dispatcher, [CancellationToken.None])!;

        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var entry = await db.DirectoryProvisioningOutbox.AsNoTracking().SingleAsync(item => item.Id == jobId);
            Assert.Null(entry.CompletedAt);
            Assert.Equal(0, entry.Attempts);
            Assert.Null(entry.LastError);
        }
    }
}
