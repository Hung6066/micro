namespace His.Hope.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public int WorkerCount { get; set; } = 2;
    public int BatchSize { get; set; } = 100;
    public int PollingIntervalMilliseconds { get; set; } = 250;
    public int MaxRetries { get; set; } = 8;
    public int ClaimLeaseSeconds { get; set; } = 120;

    public void Validate()
    {
        WorkerCount = Math.Clamp(WorkerCount, 1, 32);
        BatchSize = Math.Clamp(BatchSize, 1, 1000);
        PollingIntervalMilliseconds = Math.Clamp(PollingIntervalMilliseconds, 25, 30_000);
        MaxRetries = Math.Clamp(MaxRetries, 1, 20);
        ClaimLeaseSeconds = Math.Clamp(ClaimLeaseSeconds, 15, 3600);
    }
}
