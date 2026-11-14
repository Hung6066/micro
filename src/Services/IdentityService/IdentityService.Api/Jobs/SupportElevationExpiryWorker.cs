using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Api.Jobs;

/// <summary>
/// Closes expired JIT support elevations and revokes the operator's sessions.
/// The request-time guard remains authoritative; this worker provides the
/// lifecycle cleanup and audit trail required for automatic expiry.
/// </summary>
public sealed class SupportElevationExpiryWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SupportElevationExpiryWorker> _logger;

    public SupportElevationExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SupportElevationExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Support elevation expiry worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireElevationsAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Support elevation expiry sweep failed");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }

    private async Task ExpireElevationsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var tokenBlacklist = scope.ServiceProvider.GetRequiredService<ITokenBlacklistService>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var now = DateTime.UtcNow;

        var elevations = await db.SupportElevations
            .Where(item => item.Status == "approved" && item.ExpiresAt <= now)
            .OrderBy(item => item.ExpiresAt)
            .Take(100)
            .ToListAsync(ct);

        foreach (var elevation in elevations)
        {
            try
            {
                await tokenBlacklist.RevokeAllUserTokensAsync(elevation.OperatorUserId.ToString(), ct);
                elevation.Status = "expired";
                await db.SaveChangesAsync(ct);

                await audit.LogPhiAccessAsync(new PhiAuditEntry
                {
                    UserId = elevation.OperatorUserId.ToString(),
                    ResourceType = "SupportElevation",
                    ResourceId = elevation.Id.ToString("D"),
                    Action = "EXPIRE",
                    TenantId = elevation.TargetTenant,
                    HttpMethod = "WORKER",
                    Path = "support-elevations/expiry"
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to expire support elevation {ElevationId}; it will be retried",
                    elevation.Id);
            }
        }
    }
}
