namespace SystemDashboard.Bff.Models;

public sealed record MetricSnapshot
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string Unit { get; init; }
    public double CurrentValue { get; init; }
    public double? PreviousValue { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public double? Avg { get; init; }
    public List<MetricDataPoint>? DataPoints { get; init; }

    public static MetricSnapshot Empty(string name, string displayName, string unit) => new()
    {
        Name = name,
        DisplayName = displayName,
        Unit = unit,
        CurrentValue = 0,
        DataPoints = []
    };
}

public sealed record MetricDataPoint
{
    public DateTime Timestamp { get; init; }
    public double Value { get; init; }
    public Dictionary<string, string>? Labels { get; init; }
}
