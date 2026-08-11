using FluentAssertions;
using His.Hope.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

public sealed class RedisConnectionFactoryTests
{
    [Fact]
    public void CreateOptions_preserves_bare_host_and_port_connection_strings()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = RedisConnectionFactory.CreateOptions("redis:6379", configuration);

        options.EndPoints.Should().ContainSingle()
            .Which.ToString().Should().Contain("redis:6379");
    }
}
