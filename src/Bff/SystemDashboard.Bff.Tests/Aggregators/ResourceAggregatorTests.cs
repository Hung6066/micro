using System.Net;
using His.Hope.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        var kubernetesMetrics = new Mock<IKubernetesPodMetricsService>();
        var runtimeEndpoints = CreateRuntimeEndpoints();

        var aggregator = new ResourceAggregator(
            consul.Object, httpClientFactory.Object, prometheus.Object, kubernetesMetrics.Object,
            Options.Create(new KubernetesOptions { Enabled = false }), cache, logger, runtimeEndpoints);

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
        var kubernetesMetrics = new Mock<IKubernetesPodMetricsService>();
        var runtimeEndpoints = CreateRuntimeEndpoints();

        var aggregator = new ResourceAggregator(
            consul.Object, httpClientFactory.Object, prometheus.Object, kubernetesMetrics.Object,
            Options.Create(new KubernetesOptions { Enabled = false }), cache, logger, runtimeEndpoints);

        var resources = await aggregator.GetAllResourcesAsync();

        var svc = resources.OfType<ServiceResource>().FirstOrDefault(r => r.Name == "identity-service");
        Assert.NotNull(svc);
        Assert.Equal("Running", svc.Status);
        Assert.Null(svc.CpuPercent);
        Assert.Null(svc.MemoryUsedMb);
    }

    private static ServiceEndpointOptions CreateRuntimeEndpoints() =>
        RuntimeConfigurationExtensions.BindServiceEndpoints(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["REDIS_URL"] = "redis://localhost:6379",
                    ["SERVICE_IDENTITY_API_URL"] = "http://localhost:5012",
                    ["SERVICE_IDENTITY_GRPC_URL"] = "http://localhost:5012",
                    ["SERVICE_PATIENT_API_URL"] = "http://localhost:5008",
                    ["SERVICE_PATIENT_GRPC_URL"] = "http://localhost:5006",
                    ["SERVICE_APPOINTMENT_API_URL"] = "http://localhost:5009",
                    ["SERVICE_APPOINTMENT_GRPC_URL"] = "http://localhost:5007",
                    ["SERVICE_CLINICAL_API_URL"] = "http://localhost:5010",
                    ["SERVICE_CLINICAL_GRPC_URL"] = "http://localhost:5005",
                    ["SERVICE_LAB_API_URL"] = "http://localhost:5018",
                    ["SERVICE_LAB_GRPC_URL"] = "http://localhost:5018",
                    ["SERVICE_BILLING_API_URL"] = "http://localhost:5022",
                    ["SERVICE_BILLING_GRPC_URL"] = "http://localhost:5022",
                    ["SERVICE_PHARMACY_API_URL"] = "http://localhost:5030",
                    ["SERVICE_PHARMACY_GRPC_URL"] = "http://localhost:5030",
                    ["SERVICE_PATIENT_BFF_URL"] = "http://localhost:5100",
                    ["SERVICE_CLINICAL_BFF_URL"] = "http://localhost:5200",
                    ["SERVICE_LAB_BFF_URL"] = "http://localhost:5300",
                    ["SERVICE_BILLING_BFF_URL"] = "http://localhost:5400",
                    ["SERVICE_PHARMACY_BFF_URL"] = "http://localhost:5500",
                    ["SERVICE_DASHBOARD_BFF_URL"] = "http://localhost:5700"
                })
                .Build(),
            "SystemDashboard.Bff");
}

public sealed class HealthyMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Healthy")
        });
}
