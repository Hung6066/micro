using His.Hope.AgentHarness.Core.Interfaces;

namespace His.Hope.AgentHarness.Infrastructure.EventBus;

/// <summary>
/// No-op event bus used as fallback when RabbitMQ is unavailable.
/// All publish/subscribe operations complete immediately without any external interaction.
/// </summary>
public class NullEventBus : IEventBus
{
    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
        => Task.CompletedTask;

    public Task SubscribeAsync<T>(string queueName, Func<T, CancellationToken, Task> handler,
        CancellationToken ct = default) where T : class
        => Task.CompletedTask;
}
