using His.Hope.Bff.Core.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;
using Xunit;

namespace His.Hope.Bff.Core.Tests.Resilience;

public class BffResiliencePipelineTests
{
    [Fact]
    public void AddBffResilience_RegistersDownstreamPipeline_WithoutError()
    {
        var services = new ServiceCollection();
        services.AddBffResilience();
        var provider = services.BuildServiceProvider();

        var pipelineProvider = provider.GetRequiredService<ResiliencePipelineProvider<string>>();
        var pipeline = pipelineProvider.GetPipeline("bff-downstream");

        Assert.NotNull(pipeline);
    }

    [Fact]
    public void AddBffResilience_RegistersAggregationPipeline_WithoutError()
    {
        var services = new ServiceCollection();
        services.AddBffResilience();
        var provider = services.BuildServiceProvider();

        var pipelineProvider = provider.GetRequiredService<ResiliencePipelineProvider<string>>();
        var pipeline = pipelineProvider.GetPipeline("bff-aggregation");

        Assert.NotNull(pipeline);
    }
}
