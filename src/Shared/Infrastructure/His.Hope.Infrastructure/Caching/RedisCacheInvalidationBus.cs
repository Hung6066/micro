using StackExchange.Redis;

namespace His.Hope.Infrastructure.Caching;

public interface ICacheInvalidationBus
{
    Task PublishPrefixAsync(string prefix, CancellationToken ct = default);
    void Subscribe(Func<string, Task> handler);
}

/// <summary>
/// Broadcasts cache-prefix invalidations so each service replica can evict its
/// process-local L1 cache after the shared Redis tier is changed.
/// </summary>
public sealed class RedisCacheInvalidationBus : ICacheInvalidationBus, IDisposable
{
    private const string ChannelName = "HisHope:cache:invalidate:v1";
    private readonly ISubscriber _subscriber;
    private readonly List<Func<string, Task>> _handlers = [];
    private readonly object _sync = new();
    private bool _subscribed;

    public RedisCacheInvalidationBus(IConnectionMultiplexer connectionMultiplexer)
    {
        _subscriber = connectionMultiplexer.GetSubscriber();
    }

    public async Task PublishPrefixAsync(string prefix, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ct.ThrowIfCancellationRequested();
        await _subscriber.PublishAsync(RedisChannel.Literal(ChannelName), prefix);
    }

    public void Subscribe(Func<string, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_sync)
        {
            _handlers.Add(handler);
            if (_subscribed) return;
            _subscribed = true;
        }

        _ = _subscriber.SubscribeAsync(RedisChannel.Literal(ChannelName), (channel, value) =>
        {
            Func<string, Task>[] handlers;
            lock (_sync) handlers = _handlers.ToArray();
            foreach (var registeredHandler in handlers)
                _ = InvokeHandlerAsync(registeredHandler, value.ToString());
        });
    }

    private static async Task InvokeHandlerAsync(Func<string, Task> handler, string prefix)
    {
        try
        {
            await handler(prefix);
        }
        catch
        {
            // Cache invalidation is best effort; a subsequent write or TTL
            // remains the recovery path if a local eviction handler fails.
        }
    }

    public void Dispose()
    {
        // The multiplexer owns the subscription lifetime.
    }
}
