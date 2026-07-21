using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.Redis;
using StackExchange.Redis;
using Xunit;

namespace PharmacyBff.IntegrationTests;

public class PharmacyBffEndToEndTests : IAsyncLifetime
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
        Environment.SetEnvironmentVariable("Services__Pharmacy", "http://localhost:5599");

        _bff = new WebApplicationFactory<Program>();
        _client = _bff.CreateClient();
    }

    [Fact]
    public async Task GetMedicationSearch_WithValidSession_ProxiesRequest()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt-token",
            Permissions = new[] { "pharmacy.view" },
            CsrfToken = "csrf-test",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:test-sid", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/medications/search");
        request.Headers.Add("Cookie", "hishop_sid=test-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task GetMedicationById_WithValidSession_ProxiesRequest()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt-token",
            Permissions = new[] { "pharmacy.view" },
            CsrfToken = "csrf-test",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:med-sid", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/medications/med-123");
        request.Headers.Add("Cookie", "hishop_sid=med-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task GetPrescriptionSearch_WithValidSession_ProxiesRequest()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt-token",
            Permissions = new[] { "pharmacy.view" },
            CsrfToken = "csrf-test",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:rx-sid", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/prescriptions/search");
        request.Headers.Add("Cookie", "hishop_sid=rx-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task GetPrescriptionById_WithValidSession_ProxiesRequest()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt-token",
            Permissions = new[] { "pharmacy.view" },
            CsrfToken = "csrf-test",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:rxget-sid", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/prescriptions/rx-123");
        request.Headers.Add("Cookie", "hishop_sid=rxget-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task AnyRoute_WithoutSession_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/medications/search");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnyRoute_WithExpiredSession_Returns401()
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
        await db.StringSetAsync("session:expired-pharm-sid", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/prescriptions/search");
        request.Headers.Add("Cookie", "hishop_sid=expired-pharm-sid");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMedication_WithMutationMethod_RequiresCsrf()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt",
            Permissions = new[] { "pharmacy.write" },
            CsrfToken = "csrf-create-med",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:create-med", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/medications")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Cookie", "hishop_sid=create-med");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateMedication_WithCsrf_ProxiesRequest()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt",
            Permissions = new[] { "pharmacy.write" },
            CsrfToken = "csrf-create-med-ok",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:create-med-ok", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/medications")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Cookie", "hishop_sid=create-med-ok");
        request.Headers.Add("X-CSRF-Token", "csrf-create-med-ok");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task PrescriptionFill_WithCsrf_ProxiesRequest()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt",
            Permissions = new[] { "pharmacy.write" },
            CsrfToken = "csrf-fill-rx",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:fill-rx", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/prescriptions/rx-123/fill");
        request.Headers.Add("Cookie", "hishop_sid=fill-rx");
        request.Headers.Add("X-CSRF-Token", "csrf-fill-rx");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task PrescriptionCancel_WithCsrf_ProxiesRequest()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt",
            Permissions = new[] { "pharmacy.write" },
            CsrfToken = "csrf-cancel-rx",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:cancel-rx", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/prescriptions/rx-123/cancel");
        request.Headers.Add("Cookie", "hishop_sid=cancel-rx");
        request.Headers.Add("X-CSRF-Token", "csrf-cancel-rx");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("test-agent", "1.0"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task MedicationUpdate_WithCsrf_ProxiesRequest()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var db = redis.GetDatabase();
        var session = new SessionData
        {
            UserId = "usr_test",
            Jwt = "test-jwt",
            Permissions = new[] { "pharmacy.write" },
            CsrfToken = "csrf-upd-med",
            UserAgentHash = ComputeSha256("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await db.StringSetAsync("session:upd-med", JsonSerializer.Serialize(session));

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/medications/med-123")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Cookie", "hishop_sid=upd-med");
        request.Headers.Add("X-CSRF-Token", "csrf-upd-med");
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
