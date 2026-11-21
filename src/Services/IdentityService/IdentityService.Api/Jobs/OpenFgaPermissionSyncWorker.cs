using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Api.Jobs;

public sealed class OpenFgaPermissionSyncWorker(
    IServiceScopeFactory scopeFactory,
    IdentityRedisLock distributedLock,
    IHttpClientFactory httpClients,
    IConfiguration configuration,
    ILogger<OpenFgaPermissionSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OpenFGA canonical permission reconciliation failed; synchronization will be retried");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }

    public async Task ReconcileAsync(CancellationToken ct)
    {
        await using var lease = await distributedLock.TryAcquireAsync(
            $"identity:openfga-permissions:{configuration["AUTHZ_OPENFGA_STORE_ID"]}",
            TimeSpan.FromMinutes(5));
        if (lease is null)
        {
            logger.LogDebug("OpenFGA permission projection is already running on another replica");
            return;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(1));
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var now = DateTime.UtcNow;
        var users = await db.Users.AsNoTracking()
            .Where(user => user.IsActive && (user.LockoutEnd == null || user.LockoutEnd <= now))
            .Select(user => user.Id)
            .ToListAsync(deadline.Token);
        var desired = new HashSet<OpenFgaPermissionTuple>();
        foreach (var userId in users)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var permissions = await identity.GetEffectivePermissionsAsync(userId, deadline.Token);
            foreach (var permission in permissions)
                desired.Add(new OpenFgaPermissionTuple(
                    $"user:{userId:D}", OpenFgaPermissionProjection.Relation, $"permission:{permission}"));
        }

        using var http = httpClients.CreateClient(OpenFgaPermissionProjection.HttpClientName);
        var projection = new OpenFgaPermissionProjection(http, configuration);
        var result = await projection.ReconcileAsync(desired, deadline.Token);
        logger.LogInformation(
            "OpenFGA canonical permission reconciliation completed: {Removed} revoked, {Added} granted",
            result.Removed, result.Added);
    }
}
