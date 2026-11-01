using His.Hope.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

[Collection("shared-redis")]
public sealed class RedisKeyNamespaceTests(RedisTestFixture fixture)
{
    [Fact]
    public async Task ConnectPrefixesStringAndStreamKeysByTheConfiguredServiceScope()
    {
        const string scope = "project:service:production:";
        const string stringKey = "his-hope:identity:probe";
        const string streamKey = "his-hope:identity:admin-jobs-probe";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:KeyPrefix"] = scope
            })
            .Build();

        const string userName = "identity-service-test";
        const string password = "test-password-for-scoped-redis";
        var endpoint = fixture.Connection.GetEndPoints().Single();
        fixture.Connection.GetServer(endpoint).Execute(
            "ACL", "SETUSER", userName, "reset", "on", $">{password}", $"~{scope}*",
            "+@read", "+@write", "+@keyspace", "+@string", "+@stream", "+@connection", "+@fast",
            "-@dangerous", "-@admin", "+info", "+ping");

        var scopedConnectionString = $"redis://{userName}:{password}@{endpoint}";
        var redis = RedisConnectionFactory.Connect(scopedConnectionString, configuration);
        var unprefixed = RedisConnectionFactory.Connect(
            scopedConnectionString,
            new ConfigurationBuilder().Build());
        try
        {
            var database = redis.GetDatabase();
            await database.StringSetAsync(stringKey, "value");
            await database.StreamCreateConsumerGroupAsync(streamKey, "workers", "0-0", createStream: true);

            var rawDatabase = fixture.Connection.GetDatabase();
            Assert.Equal("value", await rawDatabase.StringGetAsync(scope + stringKey));
            Assert.False(await rawDatabase.KeyExistsAsync(stringKey));
            Assert.True(await rawDatabase.KeyExistsAsync(scope + streamKey));
            Assert.False(await rawDatabase.KeyExistsAsync(streamKey));
            await Assert.ThrowsAsync<RedisServerException>(async () =>
            {
                await unprefixed.GetDatabase().StringSetAsync(stringKey, "must-be-denied");
            });
        }
        finally
        {
            await redis.CloseAsync();
            await unprefixed.CloseAsync();
        }
    }
}
