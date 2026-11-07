namespace His.Hope.Infrastructure.Saga;

public static class SagaStatus
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Compensating = "Compensating";
    public const string Compensated = "Compensated";

    public static bool IsTerminal(string status) =>
        status is Completed or Failed or Compensated;
}

public sealed record SagaExecutionMetadata(
    string? TenantKey = null,
    string? CorrelationId = null,
    string? CausationId = null,
    string? IdempotencyKey = null);
