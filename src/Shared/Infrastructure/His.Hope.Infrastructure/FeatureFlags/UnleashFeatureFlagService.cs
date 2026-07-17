using Microsoft.Extensions.Logging;
using Unleash;

namespace His.Hope.Infrastructure.FeatureFlags;

/// <summary>
/// Feature flag service implementation backed by Unleash.
/// Wraps the Unleash client SDK and provides graceful fallback to the default
/// value when the Unleash server is unreachable or an error occurs.
/// </summary>
public sealed class UnleashFeatureFlagService : IFeatureFlagService, IDisposable
{
    private readonly IUnleash _unleash;
    private readonly ILogger<UnleashFeatureFlagService> _logger;
    private bool _disposed;

    public UnleashFeatureFlagService(IUnleash unleash, ILogger<UnleashFeatureFlagService> logger)
    {
        _unleash = unleash ?? throw new ArgumentNullException(nameof(unleash));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync(string flagName, bool defaultValue = false)
    {
        try
        {
            // Unleash's IsEnabled is synchronous — it evaluates from an in-memory cache
            // that is kept current by a background synchronisation thread.
            var result = _unleash.IsEnabled(flagName, defaultValue);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to evaluate feature flag {FlagName}, falling back to default: {Default}",
                flagName, defaultValue);
            return Task.FromResult(defaultValue);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_unleash is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }
}
