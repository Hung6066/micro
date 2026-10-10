using System.Collections.Concurrent;
using His.Hope.Infrastructure.Backpressure;

namespace His.Hope.Infrastructure.Resilience;

public sealed class AdaptiveConcurrencyLimiterRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, AdaptiveConcurrencyLimiter> _limiters = new(
        StringComparer.OrdinalIgnoreCase);

    public AdaptiveConcurrencyLimiter Get(string dependencyName) =>
        _limiters.GetOrAdd(dependencyName, static _ => new AdaptiveConcurrencyLimiter());

    public void Dispose()
    {
        foreach (var limiter in _limiters.Values)
        {
            limiter.Dispose();
        }
    }
}
