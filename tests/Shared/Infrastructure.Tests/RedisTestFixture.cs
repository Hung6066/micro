using StackExchange.Redis;
using Testcontainers.Redis;

namespace His.Hope.Infrastructure.Tests;

public sealed class RedisTestFixture : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder().Build();
    public IConnectionMultiplexer Connection { get; private set; } = null!;
    public string ConnectionString => _redis.GetConnectionString();

    public Task StopRedisAsync() => _redis.StopAsync();

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        var options = ConfigurationOptions.Parse(_redis.GetConnectionString());
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 5;
        options.ConnectTimeout = 5000;
        Exception? last = null;
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            try
            {
                Connection = await ConnectionMultiplexer.ConnectAsync(options);
                if (Connection.IsConnected)
                    return;
            }
            catch (RedisConnectionException ex) when (attempt < 12)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(250 * attempt, 2000)));
            }
        }

        throw new TimeoutException("Redis Testcontainer was ready but the host connection did not become available.", last);
    }

    public async Task DisposeAsync()
    {
        if (Connection is not null)
            await Connection.CloseAsync();
        await _redis.DisposeAsync();
    }
}

[CollectionDefinition("shared-redis", DisableParallelization = true)]
public sealed class SharedRedisCollection : ICollectionFixture<RedisTestFixture>;
