using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Tests.Aggregators;

public sealed class ResourceAggregatorTests
{
    [Fact]
    public async Task GetAllResourcesAsync_ReturnsCachedResult_OnSecondCall()
    {
        var consul = new Mock<IConsulDiscoveryService>();
        consul.Setup(c => c.GetServiceNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["identity-service"]);
        consul.Setup(c => c.GetServiceHealthAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string serviceName, CancellationToken _) =>
                serviceName == "identity-service"
                    ? new ConsulServiceHealth { ServiceName = "identity-service", Status = "passing" }
                    : null);

        var prometheus = new Mock<IPrometheusQueryService>();
        prometheus.Setup(p => p.QueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetricDataPoint { Timestamp = DateTime.UtcNow, Value = 42.0 });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new HealthyMessageHandler()));

        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<ResourceAggregator>.Instance;

        var aggregator = new ResourceAggregator(
            consul.Object, httpClientFactory.Object, prometheus.Object, cache, logger);

        var resources1 = await aggregator.GetAllResourcesAsync();

        consul.Invocations.Clear();
        prometheus.Invocations.Clear();

        var resources2 = await aggregator.GetAllResourcesAsync();

        Assert.NotEmpty(resources1);
        Assert.Equal(resources1.Count, resources2.Count);
        consul.Verify(c => c.GetServiceNamesAsync(It.IsAny<CancellationToken>()), Times.Never);
        prometheus.Verify(p => p.QueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllResourcesAsync_ReturnsNullMetrics_WhenPrometheusFails()
    {
        var consul = new Mock<IConsulDiscoveryService>();
        consul.Setup(c => c.GetServiceNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["identity-service"]);
        consul.Setup(c => c.GetServiceHealthAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string serviceName, CancellationToken _) =>
                serviceName == "identity-service"
                    ? new ConsulServiceHealth { ServiceName = "identity-service", Status = "passing" }
                    : null);

        var prometheus = new Mock<IPrometheusQueryService>();
        prometheus.Setup(p => p.QueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Simulated failure"));

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new HealthyMessageHandler()));

        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<ResourceAggregator>.Instance;

        var aggregator = new ResourceAggregator(
            consul.Object, httpClientFactory.Object, prometheus.Object, cache, logger);

        var resources = await aggregator.GetAllResourcesAsync();

        var svc = resources.OfType<ServiceResource>().FirstOrDefault(r => r.Name == "identity-service");
        Assert.NotNull(svc);
        Assert.Equal("Running", svc.Status);
        Assert.Null(svc.CpuPercent);
        Assert.Null(svc.MemoryUsedMb);
    }
}

public sealed class HealthyMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Healthy")
        });
}
