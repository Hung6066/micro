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
    private readonly RedisContainer _redis = new RedisBuilder().Build();
    private IConnectionMultiplexer _connection = null!;
    private ServiceProvider _services = null!;
    private RedisRefreshTokenStore _store = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        _services = new ServiceCollection()
            .AddStackExchangeRedisCache(options => options.Configuration = _redis.GetConnectionString())
            .BuildServiceProvider();
        _store = new RedisRefreshTokenStore(
            _services.GetRequiredService<IDistributedCache>(),
            _connection,
            NullLogger<RedisRefreshTokenStore>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
        await _services.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task ConsumeAsync_ConcurrentUseAllowsOneRedemptionAndRevokesTheFamily()
    {
        const string refreshToken = "refresh-token-under-test";
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
