using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class LdapBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LdapBackgroundService> _logger;

    public LdapBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<LdapBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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
                    var syncService = scope.ServiceProvider.GetRequiredService<LdapSyncService>();
                    await syncService.SyncAsync(stoppingToken);
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
                var intervalMinutes = Math.Clamp((await runtime.GetLdapAsync(stoppingToken)).SyncIntervalMinutes, 1, 1440);
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
        }
    }
}
