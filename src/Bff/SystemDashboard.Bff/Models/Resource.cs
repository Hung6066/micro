using System.Text.Json.Serialization;

namespace SystemDashboard.Bff.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ServiceResource), typeDiscriminator: "service")]
[JsonDerivedType(typeof(DatabaseResource), typeDiscriminator: "database")]
[JsonDerivedType(typeof(InfrastructureResource), typeDiscriminator: "infrastructure")]
public abstract record Resource
{
    public required string Name { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Status { get; init; } = "Unknown";
    public string HealthStatus { get; init; } = "Unknown";
    public string Type { get; init; } = "unknown";
    public string? Version { get; init; }
    public List<HealthCheckResult> HealthChecks { get; init; } = [];
}

public sealed record HealthCheckResult
{
    public required string Name { get; init; }
    public required string Status { get; init; }
    public string? Output { get; init; }
}

public sealed record ServiceResource : Resource
{
    public int? HttpPort { get; init; }
    public int? GrpcPort { get; init; }
    public TimeSpan Uptime { get; init; }
    public int Replicas { get; init; } = 1;
    public double? CpuPercent { get; init; }
    public double? MemoryUsedMb { get; init; }
    public double MemoryLimitMb { get; init; }
    public List<string> Databases { get; init; } = [];
    public Dictionary<string, string> Environment { get; init; } = [];
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
}

public sealed class ConsulOptions
{
    public const string SectionName = "Consul";
    public required string Address { get; init; }
}

public sealed class DockerOptions
{
    public const string SectionName = "Docker";
    public required string ComposeProjectName { get; init; }
}

public sealed class KubernetesOptions
{
    public const string SectionName = "Kubernetes";
    public bool Enabled { get; init; }
}
