using His.Hope.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;

namespace His.Hope.Infrastructure.Tests;

public sealed class RedisConnectionFactoryTests
{
    [Fact]
    public void CreateOptions_RejectsMissingConfiguredCa()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:TlsCaFile"] = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.crt")
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RedisConnectionFactory.CreateOptions("rediss://redis:6379", configuration));

        Assert.Contains("is missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateOptions_RejectsPlaintextWhenCaIsConfigured()
    {
        var caPath = Path.GetTempFileName();
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Redis:TlsCaFile"] = caPath
                })
                .Build();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                RedisConnectionFactory.CreateOptions("redis://redis:6379", configuration));

            Assert.Contains("not using TLS", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(caPath);
        }
    }
}
