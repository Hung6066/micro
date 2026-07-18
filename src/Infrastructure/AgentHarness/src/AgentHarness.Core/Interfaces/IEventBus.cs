namespace His.Hope.AgentHarness.Core.Interfaces;

public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class;
    Task SubscribeAsync<T>(string queueName, Func<T, CancellationToken, Task> handler, CancellationToken ct = default) where T : class;
}
