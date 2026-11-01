using His.Hope.Resilience;
using Microsoft.Extensions.Options;
using Xunit;

namespace ServiceDefaults.Tests;

public sealed class ResiliencePipelineTests
{
    [Fact]
    public async Task Http_pipeline_does_not_retry_non_transient_failures()
    {
        var options = Options.Create(new HisHopeResilienceOptions
        {
            RetryCount = 3,
            RetryBaseDelayMs = 0,
            CircuitBreakerFailureThreshold = 100
        });
        var pipeline = new HisHopeResiliencePipelines(options).CreateHttp("test");
        var attempts = 0;

        var action = async () => await pipeline.ExecuteAsync<HttpResponseMessage>(
            _ =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("business failure");
            });

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Equal(1, attempts);
    }
}
