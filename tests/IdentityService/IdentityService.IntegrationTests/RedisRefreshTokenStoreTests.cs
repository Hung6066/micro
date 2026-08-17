using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class RedisRefreshTokenStoreTests : IAsyncLifetime
{
    private RedisContainer? _redis;
    private string _redisConnectionString = "";
    private IConnectionMultiplexer _connection = null!;
    private ServiceProvider _services = null!;
    private RedisRefreshTokenStore _store = null!;

    public async Task InitializeAsync()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("IDENTITY_TEST_REDIS_CONNECTION");
        if (!string.IsNullOrWhiteSpace(configuredConnection))
        {
            _redisConnectionString = configuredConnection;
        }
        else
        {
            _redis = new RedisBuilder("redis:7-alpine")
                .WithCleanUp(true)
                .Build();
            await _redis.StartAsync();
            _redisConnectionString = string.Equals(
                Environment.GetEnvironmentVariable("IDENTITY_TEST_USE_CONTAINER_IP"),
                "true",
                StringComparison.OrdinalIgnoreCase)
                ? $"{_redis.IpAddress}:6379"
                : _redis.GetConnectionString();
        }

        Exception? last = null;
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                _connection = await ConnectionMultiplexer.ConnectAsync(_redisConnectionString);
                if (_connection.IsConnected)
                    break;
            }
            catch (RedisConnectionException ex) when (attempt < 20)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt));
            }
        }

        if (_connection is null || !_connection.IsConnected)
            throw new TimeoutException("Redis test connection could not be established.", last);

        _services = new ServiceCollection()
            .AddStackExchangeRedisCache(options => options.Configuration = _redisConnectionString)
            .BuildServiceProvider();
        _store = new RedisRefreshTokenStore(
            _services.GetRequiredService<IDistributedCache>(),
            _connection,
            NullLogger<RedisRefreshTokenStore>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
        if (_services is not null)
            await _services.DisposeAsync();
        if (_redis is not null)
            await _redis.DisposeAsync();
    }

    [Fact]
    public async Task ConsumeAsync_ConcurrentUseAllowsOneRedemptionAndRevokesTheFamily()
    {
        var refreshToken = $"refresh-token-under-test-{Guid.NewGuid():N}";
        var record = new RefreshTokenRecord
        {
            UserId = "test-user",
            TokenHash = RefreshTokenRecord.ComputeHash(refreshToken),
            FamilyId = RedisRefreshTokenStore.GenerateFamilyId(),
            Generation = 1,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        await _store.StoreAsync(record);

        var results = await Task.WhenAll(
            _store.ConsumeAsync(refreshToken),
            _store.ConsumeAsync(refreshToken));

        results.Should().ContainSingle(result => result.Record != null && result.Record.TokenHash == record.TokenHash && !result.WasReused);
        results.Should().ContainSingle(result => result.Record != null && result.Record.TokenHash == record.TokenHash && result.WasReused);
        (await _store.IsFamilyRevokedAsync(record.FamilyId)).Should().BeTrue();
    }
}
