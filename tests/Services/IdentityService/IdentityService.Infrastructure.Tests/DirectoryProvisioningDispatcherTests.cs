using System.Text.Json;
using His.Hope.IdentityService.Application.Provisioning;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class DirectoryProvisioningDispatcherTests
{
    [Fact]
    public async Task Dispatcher_completes_live_job_releases_lease_and_persists_external_binding()
    {
        await using var harness = await CreateHarnessAsync(new RecordingTarget("ldap", new(true, "external-42")));
        await harness.SeedAsync(new DirectoryProvisioningOutbox
        {
            Target = "ldap",
            Operation = "create",
            ResourceType = "User",
            ResourceId = "user-42",
            PayloadJson = "{\"displayName\":\"Alice\"}",
            AvailableAt = DateTime.UtcNow.AddMinutes(-1)
        });

        await harness.Dispatcher.StartAsync(CancellationToken.None);
        var item = await harness.WaitForAsync(x => x.CompletedAt is not null);
        await harness.StopAsync();

        Assert.Equal(1, harness.Target.Calls);
        Assert.Equal("user-42", harness.Target.LastChange?.ResourceId);
        Assert.Equal("external-42", item.ExternalId);
        Assert.Null(item.LastError);
        Assert.Null(item.LeaseId);
        Assert.Null(item.LeaseUntil);
        using var scope = harness.Provider.CreateScope();
        var binding = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().DirectoryProvisioningBindings.SingleAsync();
        Assert.Equal("external-42", binding.ExternalId);
    }

    [Fact]
    public async Task Dispatcher_does_not_claim_a_job_with_an_active_lease()
    {
        await using var harness = await CreateHarnessAsync(new RecordingTarget("ldap", new(true, "external-new")));
        var activeLease = Guid.NewGuid();
        await harness.SeedAsync(
            new DirectoryProvisioningOutbox
            {
                Target = "ldap", Operation = "create", ResourceType = "User", ResourceId = "already-claimed",
                LeaseId = activeLease, LeaseUntil = DateTime.UtcNow.AddMinutes(5), AvailableAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new DirectoryProvisioningOutbox
            {
                Target = "ldap", Operation = "create", ResourceType = "User", ResourceId = "available",
                AvailableAt = DateTime.UtcNow.AddMinutes(-1)
            });

        await harness.Dispatcher.StartAsync(CancellationToken.None);
        var item = await harness.WaitForAsync(x => x.ResourceId == "available" && x.CompletedAt is not null);
        await harness.StopAsync();

        Assert.Equal("available", item.ResourceId);
        Assert.Equal(1, harness.Target.Calls);
        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var claimed = await db.DirectoryProvisioningOutbox.SingleAsync(x => x.ResourceId == "already-claimed");
        Assert.Equal(activeLease, claimed.LeaseId);
        Assert.NotNull(claimed.LeaseUntil);
    }

    [Fact]
    public async Task Dispatcher_dry_run_acknowledges_without_calling_target()
    {
        await using var harness = await CreateHarnessAsync(new RecordingTarget("ldap", new(true, "unused")), "dry-run");
        await harness.SeedAsync(new DirectoryProvisioningOutbox
        {
            Target = "ldap", Operation = "update", ResourceType = "User", ResourceId = "user-1",
            AvailableAt = DateTime.UtcNow.AddMinutes(-1)
        });

        await harness.Dispatcher.StartAsync(CancellationToken.None);
        var item = await harness.WaitForAsync(x => x.CompletedAt is not null);
        await harness.StopAsync();

        Assert.Equal(0, harness.Target.Calls);
        Assert.Equal(1, item.Attempts);
        Assert.Equal("dry_run_no_external_call", item.LastError);
        Assert.Null(item.LeaseId);
        Assert.Null(item.LeaseUntil);
    }

    private static async Task<Harness> CreateHarnessAsync(RecordingTarget target, string mode = "enabled")
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(connection));
        services.AddSingleton<IProvisioningTarget>(target);
        var provider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PROVISIONING_MODE"] = mode
        }).Build();
        var dispatcher = new DirectoryProvisioningDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(), configuration,
            NullLogger<DirectoryProvisioningDispatcher>.Instance);
        return new Harness(provider, connection, dispatcher, target);
    }

    private sealed class Harness(
        ServiceProvider provider,
        SqliteConnection connection,
        DirectoryProvisioningDispatcher dispatcher,
        RecordingTarget target) : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;
        public DirectoryProvisioningDispatcher Dispatcher { get; } = dispatcher;
        public RecordingTarget Target { get; } = target;

        public async Task SeedAsync(params DirectoryProvisioningOutbox[] entries)
        {
            using var scope = Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.DirectoryProvisioningOutbox.AddRange(entries);
            await db.SaveChangesAsync();
        }

        public async Task<DirectoryProvisioningOutbox> WaitForAsync(Func<DirectoryProvisioningOutbox, bool> predicate)
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                await Task.Delay(25);
                using var scope = Provider.CreateScope();
                var items = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
                    .DirectoryProvisioningOutbox.AsNoTracking().OrderBy(x => x.CreatedAt).ToListAsync();
                var item = items.FirstOrDefault(predicate);
                if (item is not null) return item;
            }

            throw new TimeoutException("Directory provisioning dispatcher did not process the outbox item.");
        }

        public async Task StopAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Dispatcher.StopAsync(timeout.Token);
        }

        public async ValueTask DisposeAsync()
        {
            Dispatcher.Dispose();
            await Provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class RecordingTarget(string name, ProvisioningResult result) : IProvisioningTarget
    {
        public string Name { get; } = name;
        public int Calls { get; private set; }
        public ProvisioningChange? LastChange { get; private set; }

        public Task<ProvisioningResult> ApplyAsync(ProvisioningChange change, CancellationToken ct = default)
        {
            Calls++;
            LastChange = change;
            _ = JsonDocument.Parse(change.Payload.RootElement.GetRawText());
            return Task.FromResult(result);
        }
    }
}
