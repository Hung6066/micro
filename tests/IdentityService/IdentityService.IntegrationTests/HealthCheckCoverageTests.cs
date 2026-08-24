using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class HealthCheckCoverageTests
{
    [Fact]
    public async Task Redis_health_check_fails_closed_when_redis_is_unreachable()
    {
        var type = typeof(His.Hope.IdentityService.Api.Endpoints.DirectoryProvisioningEndpoints).Assembly.GetType(
            "His.Hope.IdentityService.Api.Composition.RedisHealthCheck", throwOnError: true)!;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var check = Activator.CreateInstance(type, ["127.0.0.1:1,abortConnect=false", configuration])!;

        var task = (Task)type.GetMethod(nameof(IHealthCheck.CheckHealthAsync))!
            .Invoke(check, [new HealthCheckContext(), CancellationToken.None])!;
        await task;
        var result = (HealthCheckResult)task.GetType().GetProperty("Result")!.GetValue(task)!;

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Redis unavailable", result.Description);
        Assert.NotNull(result.Exception);
    }
}
