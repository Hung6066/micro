using Microsoft.Extensions.Logging;

namespace His.Hope.Observability;

public interface IStructuredLogger
{
    void Log(
        LogLevel level,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? properties = null);
}
