using System.Diagnostics;
using His.Hope.Infrastructure.Backpressure;
using Polly;

namespace His.Hope.Infrastructure.Resilience;

/// <summary>
/// Applies an adaptive, per-dependency concurrency gate and feeds execution
/// latency back into the dependency's limiter.
/// </summary>
internal sealed class AdaptiveConcurrencyStrategy : ResilienceStrategy
{
    private readonly AdaptiveConcurrencyLimiter _limiter;
    private readonly SemaphoreSlim _wakeUp = new(0);
    private readonly int _maxQueue;
    private int _queued;
    private int _active;

    public AdaptiveConcurrencyStrategy(AdaptiveConcurrencyLimiter limiter, int maxQueue)
    {
        _limiter = limiter;
        _maxQueue = Math.Max(0, maxQueue);
    }

    protected override async ValueTask<Outcome<TResult>> ExecuteCore<TResult, TState>(
        Func<ResilienceContext, TState, ValueTask<Outcome<TResult>>> callback,
        ResilienceContext context,
        TState state)
    {
        if (!await AcquireAsync(context.CancellationToken).ConfigureAwait(false))
        {
            return Outcome.FromException<TResult>(new InvalidOperationException(
                "Adaptive concurrency queue is full."));
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            var outcome = await callback(context, state).ConfigureAwait(false);
            _limiter.RecordLatency(ElapsedMilliseconds(started));
            return outcome;
        }
        finally
        {
            Release();
        }
    }

    private async ValueTask<bool> AcquireAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (Volatile.Read(ref _active) < _limiter.CurrentLimit &&
                Interlocked.Increment(ref _active) <= _limiter.CurrentLimit)
            {
                return true;
            }

            Interlocked.Decrement(ref _active);
            if (Interlocked.Increment(ref _queued) > _maxQueue)
            {
                Interlocked.Decrement(ref _queued);
                return false;
            }

            try
            {
                await _wakeUp.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                Interlocked.Decrement(ref _queued);
                throw;
            }

            Interlocked.Decrement(ref _queued);
        }
    }

    private void Release()
    {
        Interlocked.Decrement(ref _active);
        if (Volatile.Read(ref _queued) > 0)
        {
            _wakeUp.Release();
        }
    }

    private static long ElapsedMilliseconds(long started) =>
        (long)(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
}
