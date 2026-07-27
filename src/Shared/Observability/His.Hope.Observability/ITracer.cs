using System.Diagnostics;

namespace His.Hope.Observability;

public interface ITracer
{
    Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        IEnumerable<KeyValuePair<string, object?>>? tags = null);
}
