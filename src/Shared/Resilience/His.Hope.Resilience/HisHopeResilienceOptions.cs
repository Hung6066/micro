namespace His.Hope.Resilience;

public sealed class HisHopeResilienceOptions
{
    public int RetryCount { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 200;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerDurationMs { get; set; } = 30_000;
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxConcurrency { get; set; } = 10;
    public int MaxQueue { get; set; } = 50;
}
