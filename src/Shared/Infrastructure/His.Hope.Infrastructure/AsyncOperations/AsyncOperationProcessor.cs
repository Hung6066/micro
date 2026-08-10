using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace His.Hope.Infrastructure.AsyncOperations;

/// <summary>
/// Background service that reads from a <see cref="Channel{T}"/> and executes
/// the registered <see cref="IAsyncOperationHandler{TRequest,TResult}"/> for
/// each work item, updating the <see cref="OperationStatus"/> table throughout
/// the lifecycle.
/// </summary>
public class AsyncOperationProcessor : BackgroundService
{
    private readonly ChannelReader<AsyncOperationWorkItem> _channelReader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AsyncOperationProcessor> _logger;

    public AsyncOperationProcessor(
        ChannelReader<AsyncOperationWorkItem> channelReader,
        IServiceScopeFactory scopeFactory,
        ILogger<AsyncOperationProcessor> logger)
    {
        _channelReader = channelReader;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AsyncOperationProcessor started");

        await foreach (var workItem in _channelReader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessWorkItemAsync(workItem, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AsyncOperationProcessor shutting down — cancelling work item {OperationId}", workItem.OperationId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing work item {OperationId}", workItem.OperationId);
            }
        }
    }

    private async Task ProcessWorkItemAsync(AsyncOperationWorkItem workItem, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OperationStatusDbContext>();
        var handlerResolver = scope.ServiceProvider.GetRequiredService<AsyncOperationHandlerResolver>();

        // Mark as Processing
        var record = await dbContext.OperationStatuses.FindAsync([workItem.OperationId], ct);
        if (record is null)
        {
            _logger.LogWarning("Operation {OperationId} not found in database — skipping", workItem.OperationId);
            return;
        }

        record.Status = OperationStatusValue.Processing;
        await dbContext.SaveChangesAsync(ct);

        // Resolve and execute the handler
        var progress = new Progress<int>(async progressValue =>
        {
            try
            {
                using var scope2 = _scopeFactory.CreateScope();
                var ctx = scope2.ServiceProvider.GetRequiredService<OperationStatusDbContext>();
                var op = await ctx.OperationStatuses.FindAsync([workItem.OperationId]);
                if (op is not null)
                {
                    op.Progress = progressValue;
                    await ctx.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update progress for operation {OperationId}", workItem.OperationId);
            }
        });

        try
        {
            var result = await handlerResolver.ExecuteAsync(workItem, progress, ct);

            record.Status = OperationStatusValue.Completed;
            record.Progress = 100;
            record.ResultData = JsonConvert.SerializeObject(result);
            record.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Operation {OperationId} ({Type}) completed successfully",
                workItem.OperationId, workItem.OperationType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Operation {OperationId} ({Type}) failed",
                workItem.OperationId, workItem.OperationType);

            record.Status = OperationStatusValue.Failed;
            record.ErrorMessage = ex.ToString();
            record.CompletedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
