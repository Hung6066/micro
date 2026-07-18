using Grpc.Core;
using His.Hope.Infrastructure.Degradation;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.RateLimiting;
using Polly.Retry;
using Polly.Timeout;

namespace His.Hope.Infrastructure.Resilience;

public class ResilienceConfiguration : IResiliencePipelineFactory
{
    public int RetryCount { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 200;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerDurationMs { get; set; } = 30_000;
    public int TimeoutSeconds { get; set; } = 10;
    public int BulkheadMaxParallelization { get; set; } = 10;
    public int BulkheadMaxQueuing { get; set; } = 50;

    public ResiliencePipeline GetPipeline(string dependencyName) =>
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
            .AddConcurrencyLimiter(BulkheadMaxParallelization, BulkheadMaxQueuing)
            .AddTimeout(TimeSpan.FromSeconds(TimeoutSeconds))
            .Build();

    public ResiliencePipeline GetGrpcPipeline(string dependencyName) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(RetryBaseDelayMs),
                UseJitter = true,
                ShouldHandle = args => args.Outcome switch
                {
                    { Exception: RpcException rpcEx } when IsTransientGrpcError(rpcEx) => PredicateResult.True(),
                    { Exception: HttpRequestException } => PredicateResult.True(),
                    _ => PredicateResult.False(),
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
                BreakDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
            })
            .AddConcurrencyLimiter(BulkheadMaxParallelization, BulkheadMaxQueuing)
            .AddTimeout(TimeSpan.FromSeconds(TimeoutSeconds))
            .Build();

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
                Console.WriteLine($"[{operationName}] Retry {args.AttemptNumber}/{RetryCount}");
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

        var timeout = new TimeoutStrategyOptions
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
            .AddConcurrencyLimiter(BulkheadMaxParallelization, BulkheadMaxQueuing)
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
            .AddConcurrencyLimiter(BulkheadMaxParallelization, BulkheadMaxQueuing)
            .AddTimeout(TimeSpan.FromSeconds(TimeoutSeconds))
            .Build();

    /// <summary>
    /// Builds a generic pipeline with a Polly <c>FallbackStrategy</c> as the
    /// outermost layer. When all inner strategies are exhausted, the fallback
    /// attempts to serve a stale cached response via the supplied
    /// <see cref="IDegradedResponseProvider"/>.
    /// </summary>
    public ResiliencePipeline<T> GetPipelineWithFallback<T>(
        string dependencyName,
        IDegradedResponseProvider degradedProvider) where T : class
    {
        return new ResiliencePipelineBuilder<T>()
            // Fallback is outermost — catches failures from retry, circuit breaker, etc.
            .AddFallback(new FallbackStrategyOptions<T>
            {
                ShouldHandle = args => args.Outcome switch
                {
                    { Exception: not null } => PredicateResult.True(),
                    _ => PredicateResult.False(),
                },
                FallbackAction = async args =>
                {
                    var stale = await degradedProvider.GetDegradedResponseAsync<T>(
                        dependencyName, args.Context.CancellationToken);

                    if (stale is not null)
                    {
                        return Outcome.FromResult(stale);
                    }

                    // No stale data available — rethrow the original exception
                    return Outcome.FromException<T>(args.Outcome.Exception!);
                },
                OnFallback = args =>
                {
                    Console.WriteLine(
                        "[{Dependency}] Downstream failure — attempting stale cache fallback",
                        dependencyName);
                    return default;
                },
            })
            .AddRetry(new RetryStrategyOptions<T>
            {
                MaxRetryAttempts = RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(RetryBaseDelayMs),
                UseJitter = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
            {
                FailureRatio = 0.5,
                MinimumThroughput = CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
                BreakDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
            })
            .AddConcurrencyLimiter(BulkheadMaxParallelization, BulkheadMaxQueuing)
            .AddTimeout(TimeSpan.FromSeconds(TimeoutSeconds))
            .Build();
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
