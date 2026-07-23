using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class LdapBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<LdapBackgroundService> _logger;

    public LdapBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<LdapBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue<bool>("Ldap:Enabled");
        if (!enabled)
        {
            _logger.LogInformation("LDAP background sync is disabled");
            return;
        }

        var intervalMinutes = _config.GetValue("Ldap:SyncIntervalMinutes", 15);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        _logger.LogInformation("LDAP background sync started. Interval: {Interval} minutes", intervalMinutes);

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<LdapSyncService>();
                await syncService.SyncAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LDAP background sync iteration failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
