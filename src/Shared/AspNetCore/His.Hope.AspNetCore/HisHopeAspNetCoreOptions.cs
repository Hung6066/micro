using His.Hope.SharedKernel.Protocol;

namespace His.Hope.AspNetCore;

public sealed class HisHopeAspNetCoreOptions
{
    public string CorrelationHeaderName { get; set; } = HisHopeProtocolConstants.Headers.CorrelationId;

    public int MaximumCorrelationIdLength { get; set; } = 128;

    public string HealthPath { get; set; } = HisHopeProtocolConstants.Routes.Health;
}
