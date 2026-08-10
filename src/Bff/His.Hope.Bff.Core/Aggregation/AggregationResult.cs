namespace His.Hope.Bff.Core.Aggregation;

public sealed record AggregationResult
{
    public int StatusCode { get; init; } = 200;
    public object? Data { get; init; }
    public DegradedField[] Degraded { get; init; } = Array.Empty<DegradedField>();

    public static AggregationResult Success(object data) => new() { Data = data };

    public static AggregationResult Partial(object data, DegradedField[] degraded) => new()
        { Data = data, Degraded = degraded };

    public static AggregationResult Failed(string reason) => new()
        { StatusCode = 502, Data = new { error = reason } };
}

public sealed record DegradedField(string Field, string Reason, string CorrelationId);
