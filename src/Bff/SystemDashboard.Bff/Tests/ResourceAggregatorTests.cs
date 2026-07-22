using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;
using Xunit;

namespace SystemDashboard.Bff.Tests;

public sealed class ResourceAggregatorTests
{
    [Fact]
    public async Task GetAllResourcesAsync_ReturnsAllServices()
    {
        // Arrange
        var consul = Substitute.For<IConsulDiscoveryService>();
        consul.GetServiceNamesAsync(default)
            .Returns(new List<string> { "identity-service", "patient-service" });

        consul.GetServiceHealthAsync("identity-service", default)
            .Returns(new ConsulServiceHealth
            {
                ServiceName = "identity-service",
                Status = "passing",
                Port = 5001,
                Address = "127.0.0.1",
                Checks =
                [
                    new ConsulHealthCheck { Name = "Service Status", Status = "passing" }
                ]
            });

        consul.GetServiceHealthAsync("patient-service", default)
            .Returns(new ConsulServiceHealth
            {
                ServiceName = "patient-service",
                Status = "passing",
                Port = 5002,
                Address = "127.0.0.1",
                Checks =
                [
                    new ConsulHealthCheck { Name = "Service Status", Status = "passing" }
                ]
            });

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        // Mock health-check client for services not in Consul (e.g. dashboard-bff)
        var healthyHandler = new HealthyMessageHandler();
        var healthyClient = new HttpClient(healthyHandler);
        httpClientFactory.CreateClient("health-check").Returns(healthyClient);

        var prometheus = Substitute.For<IPrometheusQueryService>();
        prometheus.QueryRangeAsync(
                Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var cache = Substitute.For<IMemoryCache>();
        var logger = Substitute.For<ILogger<ResourceAggregator>>();
        var aggregator = new ResourceAggregator(consul, httpClientFactory, prometheus, cache, logger);


        // Act
        var resources = await aggregator.GetAllResourcesAsync(default);

        // Assert
        var services = resources.OfType<ServiceResource>().ToList();
        Assert.Contains(services, s => s.Name == "identity-service");
        Assert.Contains(services, s => s.Name == "patient-service");
        Assert.Contains(services, s => s.Name == "dashboard-bff");

        var serviceWithHealth = services.First(s => s.Name == "identity-service");
        Assert.Equal("Healthy", serviceWithHealth.HealthStatus);
        Assert.Equal("Running", serviceWithHealth.Status);
        Assert.Equal(5001, serviceWithHealth.HttpPort);
        Assert.Single(serviceWithHealth.HealthChecks);

        var databases = resources.OfType<DatabaseResource>().ToList();
        Assert.NotEmpty(databases);
        Assert.Contains(databases, d => d.Name == "identitydb");
        Assert.Contains(databases, d => d.Name == "patientdb");

        var infra = resources.OfType<InfrastructureResource>().ToList();
        Assert.Contains(infra, i => i.Name == "Redis");
        Assert.Contains(infra, i => i.Name == "RabbitMQ");
    }

    [Fact]
    public async Task GetAllResourcesAsync_HandlesConsulFailure()
    {
        // Arrange
        var consul = Substitute.For<IConsulDiscoveryService>();
        consul.GetServiceNamesAsync(default)
            .Returns<List<string>>(_ => throw new HttpRequestException("Consul unavailable"));

        // Mock HttpClientFactory to simulate unreachable services
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var mockClient = new HttpClient(new FailureMessageHandler());
        httpClientFactory.CreateClient("health-check").Returns(mockClient);

        var cache = Substitute.For<IMemoryCache>();
        var logger = Substitute.For<ILogger<ResourceAggregator>>();
        var prometheus = Substitute.For<IPrometheusQueryService>();
        var aggregator = new ResourceAggregator(consul, httpClientFactory, prometheus, cache, logger);

        // Act
        var resources = await aggregator.GetAllResourcesAsync(default);

        // Assert — static _serviceMap entries are always returned (even without Consul)
        // so the services list is NOT empty; each service shows "Stopped"/"Unhealthy"
        // because the direct health-check fallback catches the connection failure.
        var services = resources.OfType<ServiceResource>().ToList();
        Assert.NotEmpty(services);
        Assert.Contains(services, s => s.Name == "identity-service");
        Assert.Contains(services, s => s.Name == "dashboard-bff");

        // All services should be Stopped/Unhealthy since Consul is down and health checks fail
        Assert.All(services, s => Assert.Equal("Unhealthy", s.HealthStatus));

        // Infrastructure resources still present
        var infra = resources.OfType<InfrastructureResource>().ToList();
        Assert.Contains(infra, i => i.Name == "Redis");
        Assert.Contains(infra, i => i.Name == "Elasticsearch");
        Assert.Contains(infra, i => i.Name == "API Gateway");

        // Database resources still present
        var databases = resources.OfType<DatabaseResource>().ToList();
        Assert.NotEmpty(databases);
        Assert.Contains(databases, d => d.Name == "harnessdb");
    }
}

/// <summary>
/// Test handler that simulates a healthy service response.
/// </summary>
public sealed class HealthyMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("Healthy")
        });
}

/// <summary>
/// Test handler that simulates connection failures (like port unreachable).
/// </summary>
public sealed class FailureMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => throw new HttpRequestException("Connection refused (simulated)");
}
