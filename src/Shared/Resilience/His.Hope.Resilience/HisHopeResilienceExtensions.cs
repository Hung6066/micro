using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Retry;

namespace His.Hope.Resilience;

public static class HisHopeResilienceExtensions
{
    public static IHttpClientBuilder UseHisHopeResilience(
        this IHttpClientBuilder builder,
        string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        return builder.AddHttpMessageHandler(serviceProvider =>
            new HisHopeResilienceHandler(
                serviceProvider.GetRequiredService<HisHopeResiliencePipelines>()
                    .CreateHttp(operationName)));
    }

    public static IServiceCollection AddHisHopeResilience(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HisHopeResilienceOptions>().Bind(configuration.GetSection("Resilience"));
        services.AddSingleton<HisHopeResiliencePipelines>();
        return services;
    }
}

public sealed class HisHopeResiliencePipelines(IOptions<HisHopeResilienceOptions> options)
{
    private readonly HisHopeResilienceOptions _options = options.Value;

    public ResiliencePipeline<HttpResponseMessage> CreateHttp(string operationName) =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = _options.RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs),
                UseJitter = true,
                ShouldHandle = args => args.Outcome switch
                {
                    { Exception: not null } => PredicateResult.True(),
                    { Result.StatusCode: >= System.Net.HttpStatusCode.InternalServerError } => PredicateResult.True(),
                    _ => PredicateResult.False(),
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                MinimumThroughput = _options.CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromMilliseconds(_options.CircuitBreakerDurationMs),
                BreakDuration = TimeSpan.FromMilliseconds(_options.CircuitBreakerDurationMs),
            })
            .AddConcurrencyLimiter(_options.MaxConcurrency, _options.MaxQueue)
            .AddTimeout(TimeSpan.FromSeconds(_options.TimeoutSeconds))
            .Build();

    public ResiliencePipeline CreateGrpc(string operationName) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs),
                UseJitter = true,
                ShouldHandle = args => args.Outcome switch
                {
                    { Exception: RpcException rpc } when IsTransient(rpc) => PredicateResult.True(),
                    { Exception: HttpRequestException } => PredicateResult.True(),
                    _ => PredicateResult.False(),
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = _options.CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromMilliseconds(_options.CircuitBreakerDurationMs),
                BreakDuration = TimeSpan.FromMilliseconds(_options.CircuitBreakerDurationMs),
            })
            .AddConcurrencyLimiter(_options.MaxConcurrency, _options.MaxQueue)
            .AddTimeout(TimeSpan.FromSeconds(_options.TimeoutSeconds))
            .Build();

    private static bool IsTransient(RpcException exception) => exception.StatusCode is
        StatusCode.DeadlineExceeded or StatusCode.ResourceExhausted or StatusCode.Unavailable or StatusCode.Aborted or StatusCode.Internal or StatusCode.Unknown;
}

public sealed class HisHopeResilienceHandler(ResiliencePipeline<HttpResponseMessage> pipeline) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        try
        {
            return await pipeline.ExecuteAsync(
                ctx => new ValueTask<HttpResponseMessage>(base.SendAsync(request, ctx.CancellationToken)), context);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }
}
