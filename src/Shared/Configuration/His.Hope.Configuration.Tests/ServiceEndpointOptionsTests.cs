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

    private static IConfiguration CreateConfiguration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
