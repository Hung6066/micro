using His.Hope.Infrastructure.Resilience;
using Polly;

namespace SystemDashboard.Bff.Services;

/// <summary>
/// A <see cref="DelegatingHandler"/> that wraps HTTP calls with a Polly
/// <see cref="ResiliencePipeline"/> (retry + circuit breaker from the shared
/// <see cref="ResilienceConfiguration"/> in His.Hope.Infrastructure).
/// </summary>
public sealed class ResiliencePipelineHandler : DelegatingHandler
{
    private readonly ResiliencePipeline _pipeline;

    public ResiliencePipelineHandler(IResiliencePipelineFactory factory, string pipelineName)
    {
        _pipeline = factory.GetPipeline(pipelineName);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _pipeline.ExecuteAsync(
            async ct => await base.SendAsync(request, ct),
            cancellationToken);
    }
}
