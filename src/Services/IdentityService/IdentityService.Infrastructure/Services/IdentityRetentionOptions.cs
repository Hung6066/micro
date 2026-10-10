namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed class IdentityRetentionOptions
{
    public int CompletedOutboxDays { get; set; } = 7;
    public int TelemetryDays { get; set; } = 30;
    public int SecurityEventDays { get; set; } = 90;
    public int DevicePostureDays { get; set; } = 7;
    public int ProcessedPushDays { get; set; } = 7;
    public int BatchSize { get; set; } = 500;
    /// <summary>Maximum records removed in one cycle to bound writer pressure.</summary>
    public int MaxRowsPerRun { get; set; } = 10_000;
    public int IntervalMinutes { get; set; } = 30;
    public int LockTtlMinutes { get; set; } = 10;
}
