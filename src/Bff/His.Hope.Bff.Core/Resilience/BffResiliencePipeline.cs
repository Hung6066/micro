using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace His.Hope.Bff.Core.Resilience;

public static class BffResiliencePipeline
{
    public static IServiceCollection AddBffResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline("bff-downstream", (builder, context) =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 1,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(100),
                    ShouldHandle = new PredicateBuilder()
                        .Handle<TimeoutException>()
                        .Handle<HttpRequestException>()
                        .Handle<RpcException>(ex => ex.StatusCode == StatusCode.Unavailable)
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    BreakDuration = TimeSpan.FromSeconds(30),
                    SamplingDuration = TimeSpan.FromSeconds(60)
                })
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(5)
                });
        });

        services.AddResiliencePipeline("bff-aggregation", (builder, context) =>
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(15)
            });
        });

        return services;
    }
}
