using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class LdapBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LdapBackgroundService> _logger;
    private readonly IdentityRedisLock? _distributedLock;

    public LdapBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<LdapBackgroundService> logger,
        IdentityRedisLock? distributedLock = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _distributedLock = distributedLock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LDAP background sync started with runtime configuration");
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runtime = scope.ServiceProvider.GetRequiredService<ExternalIdentityProviderRuntime>();
                var ldap = await runtime.GetLdapAsync(stoppingToken);
                if (ldap.Enabled)
                {
                    await using var lease = _distributedLock is null
                        ? null
                        : await _distributedLock.TryAcquireAsync("hishop:identity:ldap-sync", TimeSpan.FromMinutes(30));
                    if (lease is null)
                    {
                        _logger.LogDebug("LDAP sync is already running on another replica");
                    }
                    else
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<LdapSyncService>();
                        await syncService.SyncAsync(stoppingToken);
                    }
                }
                else
                {
                    _logger.LogDebug("LDAP background sync is disabled");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LDAP background sync iteration failed");
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var runtime = scope.ServiceProvider.GetRequiredService<ExternalIdentityProviderRuntime>();
                var delay = await ResolveSyncDelayAsync(
                    async ct => (await runtime.GetLdapAsync(ct)).SyncIntervalMinutes,
                    _logger,
                    stoppingToken);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    internal static async Task<TimeSpan> ResolveSyncDelayAsync(
        Func<CancellationToken, Task<int>> loadIntervalMinutes,
        ILogger<LdapBackgroundService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var intervalMinutes = Math.Clamp(await loadIntervalMinutes(cancellationToken), 1, 1440);
            return TimeSpan.FromMinutes(intervalMinutes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to load LDAP sync interval; retrying in five minutes");
            return TimeSpan.FromMinutes(5);
        }
    }
}
