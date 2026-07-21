namespace SystemDashboard.Bff.Models;

public abstract record Resource
{
    public required string Name { get; init; }
}

public sealed record HealthCheckResult
{
    public required string Name { get; init; }
    public required string Status { get; init; } // "passing", "warning", or "critical"
    public string? Output { get; init; }
}

public sealed record ServiceResource : Resource
{
    public int? HttpPort { get; init; }
    public int? GrpcPort { get; init; }
    public required string HealthStatus { get; init; }
    public TimeSpan Uptime { get; init; }
    public int Replicas { get; init; } = 1;
    public double CpuPercent { get; init; }
    public double MemoryUsedMb { get; init; }
    public double MemoryLimitMb { get; init; }
    public List<string> Databases { get; init; } = [];
    public Dictionary<string, string> Environment { get; init; } = [];
    public List<HealthCheckResult> HealthChecks { get; init; } = [];
}

public sealed record DatabaseResource : Resource
{
    public string Engine { get; init; } = "CockroachDB";
    public double SizeMb { get; init; }
    public int Connections { get; init; }
}

public sealed record InfrastructureResource : Resource
{
    public required string Category { get; init; }
    public required string Version { get; init; }
}
