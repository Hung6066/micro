using System.Reflection;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class SecuritySignalContractTests
{
    [Theory]
    [InlineData("logout", "https://schemas.openid.net/secevent/caep/event-type/session-revoked")]
    [InlineData("password-change", "https://schemas.openid.net/secevent/caep/event-type/credential-change")]
    public void EventTypesMapToCaepUris(string eventType, string expected)
    {
        var method = typeof(SecuritySignalDispatcher).GetMethod("MapEventType", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(expected, method!.Invoke(null, [eventType]));
    }

    [Fact]
    public async Task SetUsesSeceventTypeAndCaepEventsEnvelope()
    {
        using var rsa = RSA.Create(2048);
        var keyProvider = Mock.Of<IVaultKeyProvider>(provider =>
            provider.GetSigningKeyAsync(It.IsAny<CancellationToken>()) ==
            Task.FromResult<SecurityKey>(new RsaSecurityKey(rsa)));
        var dispatcher = new SecuritySignalDispatcher(
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IHttpClientFactory>(),
            keyProvider,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenIddict:Issuer"] = "https://identity.test"
            }).Build(),
            NullLogger<SecuritySignalDispatcher>.Instance);
        var entry = new SecuritySignalOutbox
        {
            EventType = "logout",
            Subject = "user-1",
            PayloadJson = "{\"subject\":\"user-1\"}"
        };
        var method = typeof(SecuritySignalDispatcher).GetMethod("CreateSetAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task<string>)method!.Invoke(dispatcher, [entry, "https://receiver.test", CancellationToken.None])!;
        var token = await task;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("secevent+jwt", jwt.Header.Typ);
        Assert.True(jwt.Payload.TryGetValue("events", out var events));
        Assert.Contains("session-revoked", events?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledTransmitterReturnsBeforeCreatingScopeOrCallingReceiver()
    {
        var dispatcher = new SecuritySignalDispatcher(
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<IVaultKeyProvider>(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SSF_ENABLED"] = "false"
            }).Build(),
            NullLogger<SecuritySignalDispatcher>.Instance);
        var method = typeof(SecuritySignalDispatcher).GetMethod("DispatchBatchAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var task = (Task)method!.Invoke(dispatcher, [CancellationToken.None])!;
        await task;
    }
}
