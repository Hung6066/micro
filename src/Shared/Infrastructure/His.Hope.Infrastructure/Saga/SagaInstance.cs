using System.Text.Json;

namespace His.Hope.Infrastructure.Saga;

/// <summary>
/// Represents a persistent saga instance stored in the saga_instances table.
/// Tracks execution state, step progress, and heartbeat for timeout recovery.
/// </summary>
public class SagaInstance
{
    /// <summary>Unique identifier for this saga execution.</summary>
    public Guid SagaId { get; set; }

    /// <summary>CLR type name of the saga orchestrator.</summary>
    public string SagaType { get; set; } = string.Empty;

    public string? TenantKey { get; set; }
    public string? CorrelationId { get; set; }
    public string? CausationId { get; set; }
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Current saga status.
    /// Valid values: Pending, Running, Completed, Failed, Compensating, Compensated.
    /// </summary>
    public string Status { get; set; } = SagaStatus.Pending;

    /// <summary>Index of the currently executing (or last completed) step.</summary>
    public int StepIndex { get; set; }

    public int RetryCount { get; set; }
    public long Version { get; set; }

    /// <summary>Serialized saga data payload as raw JSON.</summary>
    public string Data { get; set; } = "{}";

    /// <summary>Error message if the saga failed or is compensating.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>When the saga execution started.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>When the saga completed or was fully compensated.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Last heartbeat timestamp for staleness detection.</summary>
    public DateTime LastHeartbeat { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Deserializes the Data JSON into the specified type.
    /// </summary>
    public TData GetData<TData>() =>
        JsonSerializer.Deserialize<TData>(Data, JsonSerializerOptions) ?? throw new InvalidOperationException("Failed to deserialize saga data.");

    /// <summary>
    /// Serializes the provided data object and assigns it to <see cref="Data"/>.
    /// </summary>
    public void SetData<TData>(TData data) =>
        Data = JsonSerializer.Serialize(data, JsonSerializerOptions);

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
