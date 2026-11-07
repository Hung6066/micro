namespace His.Hope.Infrastructure.Qos;

public sealed class PriorityAdmissionController
{
    private readonly object _gate = new();
    private readonly PriorityAdmissionOptions _options;
    private readonly List<Waiter> _waiters = [];
    private readonly long[] _activeByPriority = new long[5];
    private long _active;
    private long _sequence;

    public PriorityAdmissionController(PriorityAdmissionOptions options)
    {
        _options = options;
    }

    public ValueTask<PriorityAdmissionLease?> AcquireAsync(
        int rank,
        CancellationToken cancellationToken)
    {
        rank = Math.Clamp(rank, 0, 4);
        lock (_gate)
        {
            if (_waiters.Count == 0 && CanAdmitNew(rank))
                return ValueTask.FromResult<PriorityAdmissionLease?>(Grant(rank));

            if (_waiters.Count >= Math.Max(0, _options.QueueCapacity))
                return ValueTask.FromResult<PriorityAdmissionLease?>(null);

            var waiter = new Waiter(rank, Interlocked.Increment(ref _sequence));
            _waiters.Add(waiter);
            DrainLocked();
            return WaitForGrantAsync(waiter, cancellationToken);
        }
    }

    private async ValueTask<PriorityAdmissionLease?> WaitForGrantAsync(
        Waiter waiter,
        CancellationToken cancellationToken)
    {
        var timeout = Task.Delay(
            TimeSpan.FromMilliseconds(Math.Max(1, _options.MaxWaitMilliseconds)),
            cancellationToken);
        var completed = await Task.WhenAny(waiter.Grant.Task, timeout);
        if (completed == waiter.Grant.Task)
            return await waiter.Grant.Task;

        lock (_gate)
        {
            if (_waiters.Remove(waiter))
                return null;
        }

        return await waiter.Grant.Task;
    }

    private bool CanAdmitNew(int rank)
    {
        if (_active >= Math.Max(1, _options.MaxConcurrentRequests))
            return false;

        var reserve = ReservedSlots();
        return rank switch
        {
            0 or 1 => true,
            2 => _active < Math.Max(0, _options.MaxConcurrentRequests - reserve / 2),
            _ => _active < Math.Max(0, _options.MaxConcurrentRequests - reserve)
        };
    }

    private bool CanGrantQueued(Waiter waiter)
    {
        if (_active >= Math.Max(1, _options.MaxConcurrentRequests))
            return false;
        return waiter.Rank <= 1 ||
               (waiter.Rank == 2 && _active < Math.Max(0, _options.MaxConcurrentRequests - ReservedSlots() / 2)) ||
               (waiter.Rank >= 3 && _active < Math.Max(0, _options.MaxConcurrentRequests - ReservedSlots()));
    }

    private void DrainLocked()
    {
        while (_active < Math.Max(1, _options.MaxConcurrentRequests))
        {
            var next = _waiters
                .Where(CanGrantQueued)
                .OrderBy(EffectivePriority)
                .ThenBy(w => w.CreatedSequence)
                .FirstOrDefault();
            if (next is null)
                return;

            _waiters.Remove(next);
            next.Grant.TrySetResult(Grant(next.Rank));
        }
    }

    private int EffectivePriority(Waiter waiter)
    {
        var agingStep = Math.Max(1, _options.AgingStepMilliseconds);
        var ageSteps = (DateTimeOffset.UtcNow - waiter.CreatedAt).TotalMilliseconds / agingStep;
        return Math.Max(0, waiter.Rank - (int)ageSteps);
    }

    private PriorityAdmissionLease Grant(int rank)
    {
        _active++;
        _activeByPriority[rank]++;
        return new PriorityAdmissionLease(this, rank);
    }

    private int ReservedSlots() =>
        Math.Clamp(
            (int)Math.Ceiling(Math.Max(0, _options.MaxConcurrentRequests) *
                              Math.Clamp(_options.ReservedHighPriorityFraction, 0, 1)),
            0,
            Math.Max(0, _options.MaxConcurrentRequests - 1));

    internal void Release(int rank)
    {
        lock (_gate)
        {
            if (_active > 0)
                _active--;
            if (_activeByPriority[rank] > 0)
                _activeByPriority[rank]--;
            DrainLocked();
        }
    }

    private sealed class Waiter(int rank, long createdSequence)
    {
        public int Rank { get; } = rank;
        public long CreatedSequence { get; } = createdSequence;
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public TaskCompletionSource<PriorityAdmissionLease?> Grant { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public sealed class PriorityAdmissionLease : IAsyncDisposable, IDisposable
{
    private readonly PriorityAdmissionController _controller;
    private readonly int _rank;
    private int _released;

    internal PriorityAdmissionLease(PriorityAdmissionController controller, int rank)
    {
        _controller = controller;
        _rank = rank;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
            _controller.Release(_rank);
    }
}
