using His.Hope.Infrastructure.Resilience;

namespace His.Hope.Infrastructure.Tests;

public sealed class ResilienceConfigurationTests
{
    [Fact]
    public async Task Adaptive_pipeline_records_latency_per_dependency()
    {
        using var registry = new AdaptiveConcurrencyLimiterRegistry();
        var configuration = new ResilienceConfiguration(registry)
        {
            RetryBaseDelayMs = 0,
            CircuitBreakerFailureThreshold = 100
        };
        var pipeline = configuration.GetPipeline("patient-service");

        for (var i = 0; i < 100; i++)
        {
            await pipeline.ExecuteAsync(static _ => ValueTask.CompletedTask);
        }

        registry.Get("patient-service").BaselineEstablished.Should().BeTrue();
        registry.Get("patient-service").BaselineP99.Should().BeGreaterThanOrEqualTo(0);
        registry.Get("patient-service").Should().BeSameAs(registry.Get("PATIENT-SERVICE"));
        registry.Get("billing-service").Should().NotBeSameAs(registry.Get("patient-service"));
    }

    [Fact]
    public async Task Generic_pipeline_does_not_retry_non_transient_failures()
    {
        var configuration = new ResilienceConfiguration
        {
            RetryCount = 3,
            RetryBaseDelayMs = 0,
            CircuitBreakerFailureThreshold = 100
        };
        var attempts = 0;

        var action = async () => await configuration.GetPipeline("test").ExecuteAsync(
            _ =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("business failure");
            });

        await FluentAssertions.FluentActions.Awaiting(action)
            .Should().ThrowAsync<InvalidOperationException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task Generic_pipeline_retries_transient_failures_within_bound()
    {
        var configuration = new ResilienceConfiguration
        {
            RetryCount = 3,
            RetryBaseDelayMs = 0,
            CircuitBreakerFailureThreshold = 100
        };
        var attempts = 0;

        var action = async () => await configuration.GetPipeline("test").ExecuteAsync(
            _ =>
            {
                Interlocked.Increment(ref attempts);
                throw new HttpRequestException("transient failure");
            });

        await FluentAssertions.FluentActions.Awaiting(action)
            .Should().ThrowAsync<HttpRequestException>();
        attempts.Should().Be(4);
    }
}
