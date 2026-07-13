using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions;
using Polly.Retry;
using Polly.Timeout;

namespace His.Hope.Infrastructure.Resilience;

public class ResilienceConfiguration
{
    public int RetryCount { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 200;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerDurationMs { get; set; } = 30_000;
    public int TimeoutSeconds { get; set; } = 10;
    public int BulkheadMaxParallelization { get; set; } = 10;
    public int BulkheadMaxQueuing { get; set; } = 50;

    public ResiliencePipeline<HttpResponseMessage> BuildHttpPipeline(string operationName)
    {
        var retry = new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = RetryCount,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(RetryBaseDelayMs),
            UseJitter = true,
            ShouldHandle = args => args.Outcome switch
            {
                { Exception: not null } => PredicateResult.True(),
                { Result.StatusCode: >= System.Net.HttpStatusCode.InternalServerError } => PredicateResult.True(),
                _ => PredicateResult.False(),
            },
            OnRetry = args =>
            {
                Console.WriteLine($"[{operationName}] Retry {args.AttemptNumber}/{RetryCount} after {args.Delay.TotalMilliseconds}ms");
                return default;
            },
        };

        var circuitBreaker = new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = 0.5,
            MinimumThroughput = CircuitBreakerFailureThreshold,
            SamplingDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
            BreakDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
            ShouldHandle = args => args.Outcome switch
            {
                { Exception: not null } => PredicateResult.True(),
                { Result.StatusCode: >= System.Net.HttpStatusCode.InternalServerError } => PredicateResult.True(),
                _ => PredicateResult.False(),
            },
            OnOpened = args =>
            {
                Console.WriteLine($"[{operationName}] Circuit breaker opened for {args.BreakDuration.TotalSeconds}s");
                return default;
            },
            OnClosed = args =>
            {
                Console.WriteLine($"[{operationName}] Circuit breaker closed");
                return default;
            },
        };

        var timeout = new TimeoutStrategyOptions<HttpResponseMessage>
        {
            Timeout = TimeSpan.FromSeconds(TimeoutSeconds),
            OnTimeout = args =>
            {
                Console.WriteLine($"[{operationName}] Timeout after {args.Timeout.TotalSeconds}s");
                return default;
            },
        };

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(retry)
            .AddCircuitBreaker(circuitBreaker)
            .AddTimeout(timeout)
            .Build();
    }

    public ResiliencePipeline BuildGenericPipeline(string operationName) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(RetryBaseDelayMs),
                UseJitter = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
                BreakDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
            })
            .AddTimeout(TimeSpan.FromSeconds(TimeoutSeconds))
            .Build();
}
