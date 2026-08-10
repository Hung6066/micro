namespace SystemDashboard.Bff.Services;

public interface IConsulDiscoveryService
{
    Task<List<string>> GetServiceNamesAsync(CancellationToken ct = default);
    Task<ConsulServiceHealth?> GetServiceHealthAsync(string serviceName, CancellationToken ct = default);
}

public sealed record ConsulServiceHealth
{
    public required string ServiceName { get; init; }
    public required string Status { get; init; }
    public int Port { get; init; }
    public string? Address { get; init; }
    public List<ConsulHealthCheck> Checks { get; init; } = [];
}

public sealed record ConsulHealthCheck
{
    public required string Name { get; init; }
    public required string Status { get; init; }
    public string? Output { get; init; }
}
