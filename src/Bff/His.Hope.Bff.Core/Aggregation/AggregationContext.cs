namespace His.Hope.Bff.Core.Aggregation;

public sealed record AggregationContext(
    IReadOnlyDictionary<string, string> RouteValues,
    string SessionJwt,
    CancellationToken CancellationToken);
