namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed class IdentityRetentionOptions
{
    public int CompletedOutboxDays { get; set; } = 7;
    public int TelemetryDays { get; set; } = 30;
    public int SecurityEventDays { get; set; } = 90;
    public int DevicePostureDays { get; set; } = 7;
    public int ProcessedPushDays { get; set; } = 7;
    public int BatchSize { get; set; } = 500;
    public int IntervalMinutes { get; set; } = 30;
    public int LockTtlMinutes { get; set; } = 10;
}