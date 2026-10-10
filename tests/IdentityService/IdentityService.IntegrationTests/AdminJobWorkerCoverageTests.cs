using System.Reflection;
using His.Hope.Contracts.Bulk;
using His.Hope.IdentityService.Api.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class AdminJobWorkerCoverageTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task ProcessAsync_skips_a_cancelled_job_without_creating_a_scope()
    {
        var store = GetStore();
        var state = NewState(BulkJobStatus.Cancelled);
        await store.SaveAsync(state, CancellationToken.None);

        await ProcessAsync(state.JobId, CancellationToken.None);

        var persisted = await store.GetAsync(state.JobId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(BulkJobStatus.Cancelled, persisted!.Status);
    }

    [Fact]
    public async Task ProcessAsync_marks_unsupported_job_failed_after_three_attempts()
    {
        var store = GetStore();
        var state = NewState(BulkJobStatus.Queued);
        state.Resource = "unsupported-resource";
        await store.SaveAsync(state, CancellationToken.None);

        await ProcessAsync(state.JobId, CancellationToken.None);

        var persisted = await store.GetAsync(state.JobId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(BulkJobStatus.Failed, persisted!.Status);
        Assert.Equal("job_failed", persisted.ErrorCode);
    }

    [Fact]
    public async Task ProcessAsync_propagates_cancellation_during_retry_backoff()
    {
        var store = GetStore();
        var state = NewState(BulkJobStatus.Queued);
        state.Resource = "unsupported-resource";
        await store.SaveAsync(state, CancellationToken.None);

        using var cancellation = new CancellationTokenSource();
        var processing = ProcessAsync(state.JobId, cancellation.Token);
        await Task.Delay(250);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing);

        var persisted = await store.GetAsync(state.JobId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(BulkJobStatus.Running, persisted!.Status);
    }

    [Fact]
    public async Task RedisAdminJobStore_round_trips_latest_result_and_cancel_state()
    {
        var store = GetStore();
        var state = NewState(BulkJobStatus.Queued);

        await store.CreateAndEnqueueAsync(state, CancellationToken.None);
        var latest = await store.GetLatestAsync(state.Kind, CancellationToken.None);
        Assert.NotNull(latest);
        Assert.Equal(state.JobId, latest!.JobId);

        var content = new byte[] { 1, 2, 3 };
        await store.SaveResultAsync(state, content, CancellationToken.None);
        var result = await store.GetResultAsync(state.JobId, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(content, result!.Value.Content);
        Assert.Equal(state.JobId, result.Value.State.JobId);

        await store.CancelAsync(state, CancellationToken.None);
        var cancelled = await store.GetAsync(state.JobId, CancellationToken.None);
        Assert.NotNull(cancelled);
        Assert.Equal(BulkJobStatus.Cancelled, cancelled!.Status);
        Assert.Equal("job_cancelled", cancelled.ErrorCode);

        // Terminal states are idempotent and must not be moved back to Cancelled.
        cancelled.Status = BulkJobStatus.Completed;
        await store.SaveAsync(cancelled, CancellationToken.None);
        await store.CancelAsync(cancelled, CancellationToken.None);
        Assert.Equal(BulkJobStatus.Completed, (await store.GetAsync(state.JobId, CancellationToken.None))!.Status);
    }

    private RedisAdminJobStore GetStore() =>
        fixture.Services.GetRequiredService<RedisAdminJobStore>();

    private async Task ProcessAsync(string jobId, CancellationToken ct)
    {
        var worker = new AdminJobWorker(
            GetStore(),
            fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AdminJobWorker>.Instance);
        var process = typeof(AdminJobWorker).GetMethod("ProcessAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(AdminJobWorker).FullName, "ProcessAsync");
        var task = (Task?)process.Invoke(worker, [jobId, ct])
            ?? throw new InvalidOperationException("ProcessAsync did not return a Task.");
        await task;
    }

    private static AdminJobState NewState(BulkJobStatus status) => new()
    {
        JobId = $"coverage-{Guid.NewGuid():N}",
        Kind = "action",
        Resource = "users",
        ActionId = "activate",
        RowKeys = [],
        PayloadJson = "{}",
        Status = status,
        Total = 0
    };
}
