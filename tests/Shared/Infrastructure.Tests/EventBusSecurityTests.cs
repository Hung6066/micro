using His.Hope.Infrastructure.Messaging;
using His.Hope.Infrastructure.FeatureFlags;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace His.Hope.Infrastructure.Tests;

public sealed class EventBusSecurityTests
{
    [Fact]
    public void GetPassword_RejectsMissingPassword()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EventBusSecurity.GetPassword(configuration));

        Assert.Contains("must be supplied", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetPassword_RejectsDevelopmentDefaultOutsideDevelopment()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HIS_HOPE_ENVIRONMENT"] = "production",
                ["EventBus:Password"] = "admin"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EventBusSecurity.GetPassword(configuration));

        Assert.Contains("development default", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetPassword_AllowsConfiguredPassword()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HIS_HOPE_ENVIRONMENT"] = "production",
                ["EventBus:Password"] = "random-production-secret"
            })
            .Build();

        Assert.Equal("random-production-secret", EventBusSecurity.GetPassword(configuration));
    }

    [Fact]
    public void RabbitMqConsumer_UsesExplicitEventTypeRegistry()
    {
        var repositoryRoot = Directory.GetParent(AppContext.BaseDirectory)!
            .Parent!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
        var sourcePath = Path.Combine(
            repositoryRoot,
            "src", "Shared", "EventBus", "Src", "His.Hope.EventBusRabbitMQ",
            "Implementations", "RabbitMQEventBus.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("_eventTypes", source, StringComparison.Ordinal);
        Assert.Contains("_eventTypes[eventName] = typeof(TIntegrationEvent)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AppDomain.CurrentDomain.GetAssemblies()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FeatureFlags_AreWiredThroughEnterpriseInfrastructure()
    {
        var repositoryRoot = Directory.GetParent(AppContext.BaseDirectory)!
            .Parent!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
        var sourcePath = Path.Combine(
            repositoryRoot,
            "src", "Shared", "Infrastructure", "His.Hope.Infrastructure",
            "DependencyInjection.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("services.AddFeatureFlags(configuration)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FeatureFlags_RequireConfigurationInProduction()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HIS_HOPE_ENVIRONMENT"] = "production",
                ["FeatureManagement:Required"] = "true"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddFeatureFlags(configuration));

        Assert.Contains("when feature management is enabled", exception.Message, StringComparison.Ordinal);
    }
}
