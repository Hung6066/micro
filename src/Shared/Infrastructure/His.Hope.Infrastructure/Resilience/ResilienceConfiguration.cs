using Grpc.Core;
using His.Hope.Infrastructure.Backpressure;
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
    private readonly AdaptiveConcurrencyLimiterRegistry? _limiterRegistry;

    public int RetryCount { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 200;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerDurationMs { get; set; } = 30_000;
    public int TimeoutSeconds { get; set; } = 10;
    public int BulkheadMaxParallelization { get; set; } = 10;
    public int BulkheadMaxQueuing { get; set; } = 50;

    /// <summary>
    /// Creates a new <see cref="ResilienceConfiguration"/>.
    /// </summary>
    /// <param name="limiterRegistry">
    /// Optional per-dependency adaptive concurrency limiter registry. When provided,
    /// the pipeline records latency and applies the live limit during execution.
    /// </param>
    public ResilienceConfiguration(AdaptiveConcurrencyLimiterRegistry? limiterRegistry = null)
    {
        _limiterRegistry = limiterRegistry;
    }

    private ResiliencePipelineBuilder AddAdaptiveConcurrency(
        ResiliencePipelineBuilder builder,
        string dependencyName) =>
        _limiterRegistry is null
            ? builder.AddConcurrencyLimiter(BulkheadMaxParallelization, BulkheadMaxQueuing)
            : builder.AddStrategy(_ => new AdaptiveConcurrencyStrategy(
                _limiterRegistry.Get(dependencyName), BulkheadMaxQueuing));

    public ResiliencePipeline GetPipeline(string dependencyName) =>
        AddAdaptiveConcurrency(new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(RetryBaseDelayMs),
                UseJitter = true,
                ShouldHandle = args => args.Outcome.Exception is { } exception && IsTransient(exception)
                    ? PredicateResult.True()
                    : PredicateResult.False(),
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
                BreakDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
            })
            .AddTimeout(TimeSpan.FromSeconds(TimeoutSeconds))
            , dependencyName).Build();

    public ResiliencePipeline GetGrpcPipeline(string dependencyName) =>
        AddAdaptiveConcurrency(new ResiliencePipelineBuilder()
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
            .AddTimeout(TimeSpan.FromSeconds(TimeoutSeconds))
            , dependencyName).Build();

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

        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(retry)
            .AddCircuitBreaker(circuitBreaker)
            .AddTimeout(timeout);

        return _limiterRegistry is null
            ? builder.AddConcurrencyLimiter(BulkheadMaxParallelization, BulkheadMaxQueuing).Build()
            : builder.AddStrategy(_ => new AdaptiveConcurrencyStrategy(
                _limiterRegistry.Get(operationName), BulkheadMaxQueuing)).Build();
    }

    public ResiliencePipeline BuildGenericPipeline(string operationName) =>
        AddAdaptiveConcurrency(new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(RetryBaseDelayMs),
                UseJitter = true,
                ShouldHandle = args => args.Outcome.Exception is { } exception && IsTransient(exception)
                    ? PredicateResult.True()
                    : PredicateResult.False(),
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
                BreakDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
            })
            .AddTimeout(TimeSpan.FromSeconds(TimeoutSeconds))
            , operationName).Build();

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
        var builder = new ResiliencePipelineBuilder<T>()
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
                        $"[{dependencyName}] Downstream failure — attempting stale cache fallback");
                    return default;
                },
            })
            .AddRetry(new RetryStrategyOptions<T>
            {
                MaxRetryAttempts = RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(RetryBaseDelayMs),
                UseJitter = true,
                ShouldHandle = args => args.Outcome.Exception is { } exception && IsTransient(exception)
                    ? PredicateResult.True()
                    : PredicateResult.False(),
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
            {
                FailureRatio = 0.5,
                MinimumThroughput = CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
                BreakDuration = TimeSpan.FromMilliseconds(CircuitBreakerDurationMs),
            })
            .AddTimeout(TimeSpan.FromSeconds(TimeoutSeconds));

        return _limiterRegistry is null
            ? builder.AddConcurrencyLimiter(BulkheadMaxParallelization, BulkheadMaxQueuing).Build()
            : builder.AddStrategy(_ => new AdaptiveConcurrencyStrategy(
                _limiterRegistry.Get(dependencyName), BulkheadMaxQueuing)).Build();
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

    private static bool IsTransient(Exception exception) => exception switch
    {
        HttpRequestException => true,
        TimeoutRejectedException => true,
        RpcException rpcException => IsTransientGrpcError(rpcException),
        _ => false,
    };
}
