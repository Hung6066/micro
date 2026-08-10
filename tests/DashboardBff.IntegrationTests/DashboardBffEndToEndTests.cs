using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.Redis;
using StackExchange.Redis;
using Xunit;

namespace DashboardBff.IntegrationTests;

public class DashboardBffEndToEndTests : IAsyncLifetime
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
        Environment.SetEnvironmentVariable("Services__Patient", "http://localhost:5099");
        Environment.SetEnvironmentVariable("Services__Clinical", "http://localhost:5099");
        Environment.SetEnvironmentVariable("Services__Lab", "http://localhost:5099");
        Environment.SetEnvironmentVariable("Services__Billing", "http://localhost:5099");
        Environment.SetEnvironmentVariable("Services__Pharmacy", "http://localhost:5099");
        Environment.SetEnvironmentVariable("Services__Appointment", "http://localhost:5099");

        _bff = new WebApplicationFactory<Program>();
        _client = _bff.CreateClient();
    }

    private async Task SeedSessionAsync(string sessionId, string csrfToken = "csrf-test")
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt-token",
            Permissions = new[] { "dashboard.view" },
            CsrfToken = csrfToken,
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync($"session:{sessionId}", JsonSerializer.Serialize(session));
    }

    [Fact]
    public async Task GetDashboardStats_WithValidSession_Returns200()
    {
        await SeedSessionAsync("stats-test-sid");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard/stats");
        request.Headers.Add("Cookie", "hishop_sid=stats-test-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(json.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task GetDashboardStats_WithoutSession_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/dashboard/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboardStats_WithExpiredSession_Returns401()
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
        await db.StringSetAsync("session:expired-stats", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard/stats");
        request.Headers.Add("Cookie", "hishop_sid=expired-stats");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRecentEncounters_WithValidSession_Returns200()
    {
        await SeedSessionAsync("encounters-test-sid");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard/recent-encounters");
        request.Headers.Add("Cookie", "hishop_sid=encounters-test-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUpcomingAppointments_WithValidSession_Returns200()
    {
        await SeedSessionAsync("appointments-test-sid");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard/upcoming-appointments");
        request.Headers.Add("Cookie", "hishop_sid=appointments-test-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
