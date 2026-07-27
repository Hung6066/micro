namespace His.Hope.Observability;

public interface IMetrics
{
    void Increment(
        string name,
        long value = 1,
        IReadOnlyDictionary<string, object?>? tags = null);

    void Record(
        string name,
        double value,
        string? unit = null,
        IReadOnlyDictionary<string, object?>? tags = null);
}
