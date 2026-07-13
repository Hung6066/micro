using Grpc.Core;
using Polly;

namespace His.Hope.Infrastructure.Resilience;

public class GrpcResilienceHandler : DelegatingHandler
{
    private readonly ResiliencePipeline _pipeline;

    public GrpcResilienceHandler(ResiliencePipeline pipeline) =>
        _pipeline = pipeline;

    public GrpcResilienceHandler(ResilienceConfiguration config)
    {
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                MaxRetryAttempts = config.RetryCount,
                BackoffType = Polly.DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(config.RetryBaseDelayMs),
                UseJitter = true,
                ShouldHandle = args => args.Outcome switch
                {
                    { Exception: RpcException rpcEx } when IsTransientGrpcError(rpcEx) => PredicateResult.True(),
                    { Exception: HttpRequestException } => PredicateResult.True(),
                    _ => PredicateResult.False(),
                },
            })
            .AddCircuitBreaker(new()
            {
                FailureRatio = 0.5,
                MinimumThroughput = config.CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromMilliseconds(config.CircuitBreakerDurationMs),
                BreakDuration = TimeSpan.FromMilliseconds(config.CircuitBreakerDurationMs),
            })
            .AddTimeout(TimeSpan.FromSeconds(config.TimeoutSeconds))
            .Build();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _pipeline.ExecuteAsync(
            async ct => await base.SendAsync(request, ct),
            cancellationToken);
    }

    private static bool IsTransientGrpcError(RpcException ex) =>
        ex.StatusCode switch
        {
            StatusCode.DeadlineExceeded => true,
            StatusCode.ResourceExhausted => true,
            StatusCode.Unavailable => true,
            StatusCode.Aborted => true,
            StatusCode.Internal => true,
            StatusCode.Unknown => true,
            _ => false,
        };
}
