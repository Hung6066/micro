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

        var logger = Substitute.For<ILogger<ResourceAggregator>>();
        var aggregator = new ResourceAggregator(consul, logger);

        // Act
        var resources = await aggregator.GetAllResourcesAsync(default);

        // Assert
        var services = resources.OfType<ServiceResource>().ToList();
        Assert.Contains(services, s => s.Name == "identity-service");
        Assert.Contains(services, s => s.Name == "patient-service");
        Assert.Contains(services, s => s.Name == "dashboard-bff");

        var serviceWithHealth = services.First(s => s.Name == "identity-service");
        Assert.Equal("Running", serviceWithHealth.HealthStatus);
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

        var logger = Substitute.For<ILogger<ResourceAggregator>>();
        var aggregator = new ResourceAggregator(consul, logger);

        // Act
        var resources = await aggregator.GetAllResourcesAsync(default);

        // Assert — static _serviceMap entries are always returned (even without Consul)
        // so the services list is NOT empty; each service shows HealthStatus = "Unknown".
        var services = resources.OfType<ServiceResource>().ToList();
        Assert.NotEmpty(services);
        Assert.Contains(services, s => s.Name == "identity-service");
        Assert.Contains(services, s => s.Name == "dashboard-bff");

        // All services should be Unknown since Consul is down
        Assert.All(services, s => Assert.Equal("Unknown", s.HealthStatus));

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
