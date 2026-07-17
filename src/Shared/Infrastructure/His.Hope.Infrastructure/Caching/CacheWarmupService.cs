using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Caching;

/// <summary>
/// Defines a single warmup task that loads reference/critical data into cache
/// during application startup.
/// </summary>
public interface IWarmupTask
{
    /// <summary>
    /// Unique name for this warmup task (used in logs).
    /// </summary>
    string TaskName { get; }

    /// <summary>
    /// Relative priority. Lower values run first. Default: 100.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Invoked on service start to pre-load data.
    /// </summary>
    Task WarmupAsync(CancellationToken ct);
}

/// <summary>
/// Base record for sharing warmup task ordering defaults.
/// </summary>
public abstract record WarmupTask : IWarmupTask
{
    public abstract string TaskName { get; }
    public virtual int Priority => 100;
    public abstract Task WarmupAsync(CancellationToken ct);
}

/// <summary>
/// IHostedService that runs registered IWarmupTask implementations at startup.
/// Supports graceful cancellation and progress logging.
/// </summary>
public class CacheWarmupService : IHostedService
{
    private readonly IEnumerable<IWarmupTask> _warmupTasks;
    private readonly ILogger<CacheWarmupService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    public CacheWarmupService(
        IEnumerable<IWarmupTask> warmupTasks,
        ILogger<CacheWarmupService> logger,
        IHostApplicationLifetime appLifetime)
    {
        _warmupTasks = warmupTasks;
        _logger = logger;
        _appLifetime = appLifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tasks = _warmupTasks
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.TaskName)
            .ToList();

        if (tasks.Count == 0)
        {
            _logger.LogInformation("No cache warmup tasks registered. Skipping warmup.");
            return;
        }

        _logger.LogInformation(
            "Cache warmup starting — {Count} task(s) registered", tasks.Count);

        var completed = 0;
        var failed = 0;

        foreach (var task in tasks)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Cache warmup cancelled before task '{TaskName}' could start",
                    task.TaskName);
                break;
            }

            try
            {
                _logger.LogDebug(
                    "Cache warmup task '{TaskName}' (priority {Priority}) starting...",
                    task.TaskName, task.Priority);

                // Apply a per-task timeout to prevent startup from hanging
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

                await task.WarmupAsync(timeoutCts.Token);

                completed++;
                _logger.LogInformation(
                    "Cache warmup task '{TaskName}' completed successfully ({Completed}/{Total})",
                    task.TaskName, completed + failed, tasks.Count);
            }
            catch (OperationCanceledException)
            {
                failed++;
                _logger.LogWarning(
                    "Cache warmup task '{TaskName}' timed out or was cancelled ({Completed}/{Total})",
                    task.TaskName, completed + failed, tasks.Count);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "Cache warmup task '{TaskName}' failed ({Completed}/{Total}) — continuing with remaining tasks",
                    task.TaskName, completed + failed, tasks.Count);

                // Do not rethrow — warmup failures are non-fatal to application startup
            }
        }

        if (failed == 0)
        {
            _logger.LogInformation(
                "Cache warmup completed — all {Count} task(s) succeeded", tasks.Count);
        }
        else
        {
            _logger.LogWarning(
                "Cache warmup completed — {Completed} succeeded, {Failed} failed out of {Total}",
                completed, failed, tasks.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No per-instance cleanup needed on stop.
        // Cache entries will expire naturally via their TTLs.
        return Task.CompletedTask;
    }
}
