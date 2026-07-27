namespace His.Hope.AspNetCore;

public sealed class HisHopeAspNetCoreOptions
{
    public string CorrelationHeaderName { get; set; } = "X-Correlation-Id";

    public int MaximumCorrelationIdLength { get; set; } = 128;

    public string HealthPath { get; set; } = "/health";
}
