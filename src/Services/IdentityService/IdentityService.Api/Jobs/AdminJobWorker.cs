using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.Infrastructure.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Api.Jobs;

public sealed class AdminJobWorker : BackgroundService
{
    private readonly RedisAdminJobStore _store;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminJobWorker> _logger;
    private readonly string _consumer = $"identity-worker-{Environment.MachineName}-{Environment.ProcessId}";

    public AdminJobWorker(
        RedisAdminJobStore store,
        IServiceScopeFactory scopeFactory,
        ILogger<AdminJobWorker> logger)
    {
        _store = store;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _store.EnsureConsumerGroupAsync(stoppingToken);
        _logger.LogInformation("Identity admin job worker started as {Consumer}", _consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = await _store.ReadNextAsync(_consumer, stoppingToken);
                if (message is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                await ProcessAsync(message.JobId, stoppingToken);
                await _store.AcknowledgeAsync(message.MessageId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin job worker failed; retrying after backoff");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessAsync(string jobId, CancellationToken ct)
    {
        var state = await _store.GetAsync(jobId, ct);
        if (state is null || state.Status == His.Hope.Contracts.Bulk.BulkJobStatus.Cancelled) return;

        state.Status = His.Hope.Contracts.Bulk.BulkJobStatus.Running;
        await _store.SaveAsync(state, ct);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await AdminTableEndpoints.ExecuteJobAsync(
                    state,
                    scope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
                    scope.ServiceProvider.GetRequiredService<UserManager<User>>(),
                    scope.ServiceProvider.GetRequiredService<IAuditService>(),
                    _store,
                    ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < 3)
            {
                _logger.LogWarning(ex, "Retrying admin job {JobId}, attempt {Attempt}", jobId, attempt + 1);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
            catch (Exception ex)
            {
                state.Status = His.Hope.Contracts.Bulk.BulkJobStatus.Failed;
                state.ErrorCode = "job_failed";
                await _store.SaveAsync(state, ct);
                _logger.LogError(ex, "Admin job {JobId} failed permanently", jobId);
            }
        }
    }
}
