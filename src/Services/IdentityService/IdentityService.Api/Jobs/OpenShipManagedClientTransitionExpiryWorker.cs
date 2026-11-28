using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace His.Hope.IdentityService.Api.Jobs;

/// <summary>
/// Closes staged OpenShip clients after their bounded overlap window. The
/// active registration remains available if deployment never finalized.
/// </summary>
public sealed class OpenShipManagedClientTransitionExpiryWorker : BackgroundService
{
    private const int PageSize = 250;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OpenShipManagedClientTransitionExpiryWorker> _logger;

    public OpenShipManagedClientTransitionExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OpenShipManagedClientTransitionExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OpenShip managed-client transition expiry worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireTransitionsAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "OpenShip managed-client transition expiry sweep failed");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }

    private async Task ExpireTransitionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var vaultStore = scope.ServiceProvider.GetRequiredService<VaultClientSecretStore>();
        var offset = 0;
        while (true)
        {
            var pageCount = 0;
            await foreach (var application in appManager.ListAsync(PageSize, offset, ct))
            {
                pageCount++;
                var clientId = await appManager.GetClientIdAsync(application, ct);
                if (string.IsNullOrWhiteSpace(clientId)) continue;
                if (await OpenShipClientProvisioningEndpoints.ExpireTransitionCandidateAsync(
                        clientId, appManager, vaultStore, ct))
                {
                    _logger.LogInformation("Expired an unused staged OpenShip client transition for {ClientId}", clientId);
                }
            }
            if (pageCount < PageSize) break;
            offset += pageCount;
        }
    }
}
