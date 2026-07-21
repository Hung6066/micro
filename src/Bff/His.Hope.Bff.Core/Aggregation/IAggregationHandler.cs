namespace His.Hope.Bff.Core.Aggregation;

public interface IAggregationHandler
{
    string Route { get; }
    string Method { get; }
    Task<AggregationResult> HandleAsync(AggregationContext context);
}
