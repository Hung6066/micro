namespace SystemDashboard.Bff.Aggregators;

public static class CacheKeys
{
    public const string AllResources = "resources:all";
    public static string ResourceByName(string name) => $"resources:{name}";
    public static string Metrics(string service, string metrics, string range) =>
        $"metrics:{service}:{metrics}:{range}";
    public static string Logs(string? service, string? level, int size, string? searchQuery) =>
        $"logs:{service ?? "*"}:{level ?? "*"}:{size}:{searchQuery ?? "*"}";
    public static string TraceSearch(string service) => $"traces:search:{service}";
    public static string TraceDetail(string traceId) => $"traces:{traceId}";
}
