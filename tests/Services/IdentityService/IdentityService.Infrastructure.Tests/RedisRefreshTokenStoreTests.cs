using System.Collections.Concurrent;
using System.Text;
using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class RedisRefreshTokenStoreTests
{
    [Fact]
    public async Task ConsumeAsync_FirstConsume_ReturnsTheStoredRecord()
    {
        const string refreshToken = "refresh-token-first-consume";
        var cache = new InMemoryDistributedCache();
        var (store, _) = CreateStore(cache, true);
        var record = CreateRecord(refreshToken);
        await store.StoreAsync(record);

        var result = await store.ConsumeAsync(refreshToken);

        result.Record.Should().BeEquivalentTo(record);
        result.WasReused.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeAsync_SecondConsume_FlagsReuseAndRevokesFamily()
    {
        const string refreshToken = "refresh-token-reused";
        var cache = new InMemoryDistributedCache();
        var (store, database) = CreateStore(cache, true, false);
        var record = CreateRecord(refreshToken);
        await store.StoreAsync(record);

        await store.ConsumeAsync(refreshToken);
        var result = await store.ConsumeAsync(refreshToken);

        result.Record.Should().BeEquivalentTo(record);
        result.WasReused.Should().BeTrue();
        (await store.IsFamilyRevokedAsync(record.FamilyId)).Should().BeTrue();
        database.Verify(
            x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                When.NotExists),
            Times.Exactly(2));
    }

    [Fact]
    public async Task IsFamilyRevokedAsync_WhenFamilyMarkerExists_ReturnsTrue()
    {
        var cache = new InMemoryDistributedCache();
        var (store, _) = CreateStore(cache, true);
        const string familyId = "family-marker-test";

        await store.RevokeFamilyAsync(familyId);

        (await store.IsFamilyRevokedAsync(familyId)).Should().BeTrue();
        cache.Keys.Should().Contain("HisHope:token_family:" + familyId);
    }

    [Fact]
    public void RefreshTokenRecord_StoresOnlyHashMaterial()
    {
        const string refreshToken = "raw-refresh-token-must-not-be-stored";

        var hash = RefreshTokenRecord.ComputeHash(refreshToken);
        var record = CreateRecord(refreshToken);

        hash.Should().NotBe(refreshToken);
        record.TokenHash.Should().Be(hash);
        record.TokenHash.Should().NotContain(refreshToken);
    }

    private static RefreshTokenRecord CreateRecord(string refreshToken) => new()
    {
        UserId = "test-user",
        TokenHash = RefreshTokenRecord.ComputeHash(refreshToken),
        FamilyId = "test-family",
        Generation = 1,
        ExpiresAt = DateTime.UtcNow.AddMinutes(5)
    };

    private static (RedisRefreshTokenStore Store, Mock<IDatabase> Database) CreateStore(
        InMemoryDistributedCache cache,
        params bool[] claimedResults)
    {
        var resultQueue = new ConcurrentQueue<bool>(claimedResults);
        var database = new Mock<IDatabase>();
        database
            .Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                When.NotExists))
            .ReturnsAsync(() => resultQueue.TryDequeue(out var claimed) && claimed);

        var redis = new Mock<IConnectionMultiplexer>();
        redis
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        return (
            new RedisRefreshTokenStore(cache, redis.Object, NullLogger<RedisRefreshTokenStore>.Instance),
            database);
    }

    private sealed class InMemoryDistributedCache : IDistributedCache
    {
        private readonly ConcurrentDictionary<string, byte[]> _entries = new();

        public IEnumerable<string> Keys => _entries.Keys;

        public byte[]? Get(string key) =>
            _entries.TryGetValue(key, out var value) ? value.ToArray() : null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            _entries[key] = value.ToArray();

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            Task.CompletedTask;

        public void Remove(string key) => _entries.TryRemove(key, out _);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }
}
