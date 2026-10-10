using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Redis;
using StackExchange.Redis;
using Xunit;

namespace BillingBff.IntegrationTests;

public class BillingBffEndToEndTests : IAsyncLifetime
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
        Environment.SetEnvironmentVariable("REDIS_URL", $"redis://{_redis.GetConnectionString()}");
        Environment.SetEnvironmentVariable("SERVICE_BILLING_API_URL", "http://localhost:5099");
        Environment.SetEnvironmentVariable("SERVICE_BILLING_GRPC_URL", "http://localhost:5099");

        _bff = new WebApplicationFactory<Program>();
        _client = _bff.CreateClient();
    }

    [Fact]
    public async Task GetInvoicesSearch_WithValidSession_ProxiesRequest()
    {
        var redis = _bff.Services.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = string.Empty,
            Permissions = new[] { "billing.view" },
            CsrfToken = "csrf-test",
            UserAgentHash = ComputeSha256("test-agent/1.0"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:test-sid", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/invoices/search");
        request.Headers.Add("Cookie", "hishop_sid=test-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task GetInvoices_WithoutSession_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/invoices/search");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetInvoices_WithExpiredSession_Returns401()
    {
        var redis = _bff.Services.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = string.Empty,
            Permissions = Array.Empty<string>(),
            CsrfToken = "csrf",
            UserAgentHash = ComputeSha256("test-agent/1.0"),
            IssuedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        await db.StringSetAsync("session:expired-sid", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/invoices/search");
        request.Headers.Add("Cookie", "hishop_sid=expired-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _bff.DisposeAsync();
        await _redis.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", null);
        Environment.SetEnvironmentVariable("REDIS_URL", null);
        Environment.SetEnvironmentVariable("SERVICE_BILLING_API_URL", null);
        Environment.SetEnvironmentVariable("SERVICE_BILLING_GRPC_URL", null);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

}
