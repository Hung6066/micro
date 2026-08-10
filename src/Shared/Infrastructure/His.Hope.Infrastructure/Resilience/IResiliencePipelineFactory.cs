using His.Hope.Infrastructure.Degradation;
using Polly;

namespace His.Hope.Infrastructure.Resilience;

public interface IResiliencePipelineFactory
{
    ResiliencePipeline GetPipeline(string dependencyName);
    ResiliencePipeline GetGrpcPipeline(string dependencyName);

    /// <summary>
    /// Builds a generic pipeline with a Polly <c>FallbackStrategy</c> as the
    /// outermost layer. When all inner strategies (retry, circuit breaker, etc.)
    /// are exhausted, the fallback attempts to serve a stale cached response
    /// via the supplied <see cref="IDegradedResponseProvider"/>.
    /// </summary>
    ResiliencePipeline<T> GetPipelineWithFallback<T>(
        string dependencyName,
        IDegradedResponseProvider degradedProvider) where T : class;
}
