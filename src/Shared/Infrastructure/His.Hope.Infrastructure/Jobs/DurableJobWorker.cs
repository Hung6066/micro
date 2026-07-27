using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Jobs;

public sealed record DurableJobProgress(int Processed, int Total);

public interface IDurableJobHandler
{
    Task ExecuteAsync(
        DurableJobState state,
        IProgress<DurableJobProgress> progress,
        CancellationToken ct);
}

/// <summary>
/// Replica-safe worker loop. Redis consumer groups deliver work and the store
/// lease prevents reclaimed or duplicate deliveries from executing twice.
/// </summary>
public sealed class DurableJobWorker : BackgroundService
{
    private readonly RedisDurableJobStore _store;
    private readonly IDurableJobHandler _handler;
    private readonly ILogger<DurableJobWorker> _logger;
    private readonly string _consumer;

    public DurableJobWorker(
        RedisDurableJobStore store,
        IDurableJobHandler handler,
        ILogger<DurableJobWorker> logger,
        string? consumer = null)
    {
        _store = store;
        _handler = handler;
        _logger = logger;
        _consumer = consumer ?? $"worker-{Environment.MachineName}-{Environment.ProcessId}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _store.EnsureConsumerGroupAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            DurableJobMessage? message = null;
            try
            {
                message = await _store.ReadNextAsync(_consumer, stoppingToken);
                if (message is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                if (!await _store.TryClaimAsync(message, _consumer, stoppingToken))
                    continue;

                var state = await _store.GetAsync(message.JobId, stoppingToken);
                if (state is null)
                {
                    await _store.RetryAsync(message, "job_state_missing", stoppingToken);
                    continue;
                }

                var progress = new Progress<DurableJobProgress>(value =>
                    _ = _store.UpdateProgressAsync(state.JobId, value.Processed, value.Total, stoppingToken));
                await _handler.ExecuteAsync(state, progress, stoppingToken);
                await _store.CompleteAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (message is not null)
                {
                    _logger.LogError(ex, "Durable job {JobId} failed; recording retry", message.JobId);
                    await _store.RetryAsync(message, ex.Message, stoppingToken);
                }
                else
                {
                    _logger.LogError(ex, "Durable job worker failed while polling");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
    }
}
