namespace His.Hope.Infrastructure.Observability;

public class CorrelationContext
{
    private static readonly AsyncLocal<string> _correlationId = new();

    public static string CurrentId
    {
        get => _correlationId.Value ?? "unknown";
        set => _correlationId.Value = value;
    }

    public bool HasCorrelationId => !string.IsNullOrEmpty(_correlationId.Value);
}
