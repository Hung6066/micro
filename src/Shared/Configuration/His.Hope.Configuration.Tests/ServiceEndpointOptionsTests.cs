using His.Hope.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace His.Hope.Configuration.Tests;

public sealed class ServiceEndpointOptionsTests
{
    [Fact]
    public void BindServiceEndpoints_BindsLogicalServiceUris_AndNormalizesTrailingSlash()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SERVICE_PATIENT_GRPC_URL"] = "http://patientservice:5006",
            ["REDIS_URL"] = "redis://localhost:6379"
        });

        var endpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(configuration, "PatientBff");

        Assert.Equal(new Uri("http://patientservice:5006/"), endpoints.GetRequired("patient-grpc"));
        Assert.Equal(new Uri("redis://localhost:6379/"), endpoints.GetRequired("redis"));
    }

    [Fact]
    public void BindServiceEndpoints_RejectsMalformedUris()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SERVICE_PATIENT_GRPC_URL"] = "not-a-uri",
            ["REDIS_URL"] = "redis://localhost:6379"
        });

        var exception = Assert.Throws<OptionsValidationException>(() =>
            RuntimeConfigurationExtensions.BindServiceEndpoints(configuration, "PatientBff"));

        Assert.Contains("malformed absolute URI", string.Join(Environment.NewLine, exception.Failures));
    }

    [Fact]
    public void BindServiceEndpoints_RejectsProductionLocalhost()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["HIS_HOPE_ENVIRONMENT"] = "production",
            ["SERVICE_PATIENT_GRPC_URL"] = "http://localhost:5006",
            ["REDIS_URL"] = "redis://redis.internal:6379"
        });

        var exception = Assert.Throws<OptionsValidationException>(() =>
            RuntimeConfigurationExtensions.BindServiceEndpoints(configuration, "PatientBff"));

        Assert.Contains("cannot use localhost in production", string.Join(Environment.NewLine, exception.Failures));
    }

    [Fact]
    public void BindServiceEndpoints_AllowsOptionalObservabilityEndpointsToBeUnavailable()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["REDIS_URL"] = "redis://localhost:6379",
            ["SERVICE_PROMETHEUS_REQUIRED"] = "false",
            ["SERVICE_ELASTICSEARCH_REQUIRED"] = "false"
        });

        var endpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(configuration, "SystemDashboard.Bff");

        Assert.Null(endpoints.GetOptional("prometheus"));
        Assert.Null(endpoints.GetOptional("elasticsearch"));
    }

    [Fact]
    public void ServicePluginRegistry_ExposesOnlyEnabledPlugins()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Plugins:Items:0:Key"] = "manufacturing",
            ["Plugins:Items:0:DisplayName"] = "Manufacturing",
            ["Plugins:Items:0:Enabled"] = "true",
            ["Plugins:Items:1:Key"] = "patient",
            ["Plugins:Items:1:DisplayName"] = "Patient",
            ["Plugins:Items:1:Enabled"] = "false"
        });

        var registry = new ServicePluginRegistry(configuration);

        Assert.True(registry.IsEnabled("manufacturing"));
        Assert.False(registry.IsEnabled("patient"));
        Assert.Single(registry.Enabled);
        Assert.Equal("manufacturing", registry.Enabled[0].Key);
    }

    [Fact]
    public void ServicePluginRegistry_LastDefinitionWinsCaseInsensitively()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Plugins:Items:0:Key"] = "Commerce",
            ["Plugins:Items:0:DisplayName"] = "Commerce",
            ["Plugins:Items:0:Enabled"] = "true",
            ["Plugins:Items:1:Key"] = "commerce",
            ["Plugins:Items:1:DisplayName"] = "Commerce disabled",
            ["Plugins:Items:1:Enabled"] = "false"
        });

        var registry = new ServicePluginRegistry(configuration);

        Assert.False(registry.IsEnabled("COMMERCE"));
        Assert.Equal("Commerce disabled", registry.Get("commerce")!.DisplayName);
    }

    private static IConfiguration CreateConfiguration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
