using His.Hope.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;

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
}
