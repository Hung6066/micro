using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace His.Hope.DatabaseContinuityService;

/// <summary>
/// Keeps readiness honest when encryption/continuity dependencies are down.
/// Liveness remains process-only so orchestrators can restart a stuck instance,
/// while readiness removes it from service discovery until it can accept work.
/// </summary>
public sealed class ContinuityDependenciesHealthCheck(
    IOptions<DatabaseContinuityOptions> options,
    VaultContinuityClient vault) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var value = options.Value;
        if (!value.Enabled)
            return HealthCheckResult.Healthy("Database continuity is disabled by configuration.");

        if (!value.EncryptionProvider.Contains("vault", StringComparison.OrdinalIgnoreCase))
            return HealthCheckResult.Healthy("Vault is not required by the configured encryption provider.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        var status = await vault.GetStatusAsync(timeout.Token);
        if (status is { Reachable: true, KeyVersion: not null })
            return HealthCheckResult.Healthy("Vault Transit key is reachable.");

        return HealthCheckResult.Unhealthy(
            "Vault Transit is unavailable or has no active key version.",
            data: new Dictionary<string, object>
            {
                ["configured"] = status.Configured,
                ["reachable"] = status.Reachable,
                ["sealed"] = status.Sealed,
                ["keyName"] = status.KeyName
            });
    }
}
