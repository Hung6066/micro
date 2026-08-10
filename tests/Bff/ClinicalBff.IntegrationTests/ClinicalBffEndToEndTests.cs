using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.Redis;
using StackExchange.Redis;
using Xunit;

namespace ClinicalBff.IntegrationTests;

public class ClinicalBffEndToEndTests : IAsyncLifetime
{
    private RedisContainer _redis = null!;
    private WebApplicationFactory<Program> _bff = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();
        await _redis.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("Services__Clinical", "http://localhost:5099");

        _bff = new WebApplicationFactory<Program>();
        _client = _bff.CreateClient();
    }

    [Fact]
    public async Task GetEncountersSearch_WithValidSession_ReturnsBadGateway()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt-token",
            Permissions = new[] { "clinical.view" },
            CsrfToken = "csrf-test",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:test-sid", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/encounters/search");
        request.Headers.Add("Cookie", "hishop_sid=test-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task GetEncounters_WithoutSession_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/encounters/search");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEncounters_WithExpiredSession_Returns401()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt",
            Permissions = Array.Empty<string>(),
            CsrfToken = "csrf",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        await db.StringSetAsync("session:expired-sid", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/encounters/search");
        request.Headers.Add("Cookie", "hishop_sid=expired-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEncounterFull_WithValidSession_ReturnsBadGateway()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt-token",
            Permissions = new[] { "clinical.view" },
            CsrfToken = "csrf-test",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:agg-sid", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/encounters/d1e6a7b8-c8b4-4f3e-9a2b-1c3d5e7f9a0b/full");
        request.Headers.Add("Cookie", "hishop_sid=agg-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task GetEncounterVitals_WithValidSession_ReturnsBadGateway()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt-token",
            Permissions = new[] { "clinical.view" },
            CsrfToken = "csrf-test",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:vitals-sid", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/encounters/d1e6a7b8-c8b4-4f3e-9a2b-1c3d5e7f9a0b/vitals");
        request.Headers.Add("Cookie", "hishop_sid=vitals-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _bff.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private static string ComputeSha256(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
