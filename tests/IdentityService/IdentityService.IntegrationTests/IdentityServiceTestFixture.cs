using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading.Channels;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure.Audit;
using His.Hope.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Npgsql;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public class IdentityServiceTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RedisContainer? _redis;
    private WebApplication? _app;

    public HttpClient AnonymousClient { get; private set; } = null!;
    public IServiceProvider Services => _app!.Services;
    public string PostgresConnectionString => _postgres?.GetConnectionString() ?? "";

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("hishopetest")
            .WithUsername("testuser")
            .WithPassword("testpass123!")
            .WithCleanUp(true)
            .Build();

        _redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithCleanUp(true)
            .Build();

        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        var pgConnStr = _postgres.GetConnectionString();
        var redisConnStr = _redis.GetConnectionString();
        await WaitForPostgresHostPortAsync(pgConnStr);
        using var signingRsa = RSA.Create(2048);
        using var encryptionRsa = RSA.Create(2048);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:IdentityDb"] = pgConnStr,
            ["ConnectionStrings:Redis"] = redisConnStr,
            ["Redis:ConnectionString"] = redisConnStr,
            ["OpenIddict:Issuer"] = "http://localhost",
            ["OpenIddict:AccessTokenLifetime"] = "01:00:00",
            ["OpenIddict:RefreshTokenLifetime"] = "7.00:00:00",
            ["OpenIddict:AuthorizationCodeLifetime"] = "00:01:00",
            ["OpenIddict:AllowInsecureHttp"] = "true",
            ["Jwt:Issuer"] = "http://localhost:5001",
            ["Jwt:Audience"] = "His.Hope",
            ["Jwt:Key"] = "integration-test-signing-key-32-bytes-long!",
            ["Jwt:RsaPrivateKey"] = signingRsa.ExportRSAPrivateKeyPem(),
            ["Jwt:RsaEncryptionPrivateKey"] = encryptionRsa.ExportRSAPrivateKeyPem(),
            ["RateLimiting:AuthPermitLimit"] = "120",
            ["RateLimiting:MaxRequestsPerIp"] = "1000",
            ["Vault:EnableTransit"] = "false",
            ["Vault:RequireVault"] = "false",
            ["Identity:BootstrapAdmin:Password"] = "Test@123456"
            , ["Authentication:Google:ClientId"] = "integration-google-client"
            , ["Authentication:Google:ClientSecret"] = "integration-google-secret"
        });
        builder.AddIdentityService();

        _app = builder.Build();

        _app.UseIdentityServicePipeline();
        _app.MapIdentityServiceEndpoints();
        _app.MapHisHopeHealthEndpoints();

        // Seed test admin user
        using (var scope = _app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<His.Hope.IdentityService.Domain.Entities.Role>>();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureCreatedAsync();

            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new His.Hope.IdentityService.Domain.Entities.Role
                {
                    Name = "Admin",
                    Description = "Test Admin",
                    IsSystem = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (await userManager.FindByNameAsync("admin") is null)
            {
                var adminUser = new User
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    UserName = "admin",
                    Email = "admin@hishop.com",
                    FirstName = "Test",
                    LastName = "Admin",
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(adminUser, "Test@123456");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        await _app.StartAsync();
        AnonymousClient = _app.GetTestClient();
    }

    private static async Task WaitForPostgresHostPortAsync(string connectionString)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                await connection.CloseAsync();
                return;
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                last = ex;
                await Task.Delay(250);
            }
        }

        throw new TimeoutException("PostgreSQL Testcontainer was ready internally but its mapped host port was not reachable.", last);
    }

    public async Task DisposeAsync()
    {
        AnonymousClient?.Dispose();
        if (_app is not null)
            await _app.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
        if (_redis is not null)
            await _redis.DisposeAsync();
    }

    public SessionClient CreateSessionClient()
    {
        var client = _app!.GetTestClient();
        return new SessionClient(client);
    }

    public async Task<SessionClient> CreateAuthenticatedSessionAsync()
    {
        var session = CreateSessionClient();
        var response = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (!response.IsSuccessStatusCode)
        {
            // Registration fallback
            var regResponse = await session.InnerClient.PostAsJsonAsync("/api/v1/auth/register",
                new { email = "test-user@test.test", password = "Test@123456", firstName = "Test", lastName = "User" });
            if (regResponse.IsSuccessStatusCode)
                await session.LoginAsync("test-user@test.test", "Test@123456");
        }
        return session;
    }
}

[CollectionDefinition("IdentityServiceIntegration")]
public class IdentityServiceIntegrationCollection : ICollectionFixture<IdentityServiceTestFixture>
{
}

public class SessionClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly List<Cookie> _cookies = new();

    public HttpClient InnerClient => _client;
    public string? RateLimitKey { get; set; }
    public SessionClient(HttpClient client) => _client = client;

    public async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            _cookies.Clear();
            foreach (var header in setCookieHeaders)
            {
                var parts = header.Split(';');
                var nameValue = parts[0].Split('=', 2);
                if (nameValue.Length == 2)
                    _cookies.Add(new Cookie(nameValue[0].Trim(), nameValue[1].Trim(), "/"));
            }
        }
        return response;
    }

    public string? GetCookieValue(string name) =>
        _cookies.FirstOrDefault(c => c.Name == name)?.Value;

    public async Task<HttpResponseMessage> PostWithCookiesAsync(string url, object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        foreach (var cookie in _cookies)
            request.Headers.TryAddWithoutValidation("Cookie", $"{cookie.Name}={cookie.Value}");
        var csrf = GetCookieValue("hishop_csrf");
        if (csrf is not null)
            request.Headers.Add("X-CSRF-Token", csrf);
        if (RateLimitKey is not null)
            request.Headers.Add("X-RateLimit-Key", RateLimitKey);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> GetWithCookiesAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var cookie in _cookies)
            request.Headers.TryAddWithoutValidation("Cookie", $"{cookie.Name}={cookie.Value}");
        var csrf = GetCookieValue("hishop_csrf");
        if (csrf is not null)
            request.Headers.Add("X-CSRF-Token", csrf);
        return await _client.SendAsync(request);
    }

    public void ApplySetCookieHeaders(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            foreach (var header in setCookieHeaders)
            {
                var parts = header.Split(';');
                var nameValue = parts[0].Split('=', 2);
                if (nameValue.Length == 2)
                {
                    _cookies.RemoveAll(c => c.Name == nameValue[0].Trim());
                    _cookies.Add(new Cookie(nameValue[0].Trim(), nameValue[1].Trim(), "/"));
                }
            }
        }
    }

    public void SetCookieValue(string name, string value)
    {
        _cookies.RemoveAll(c => c.Name == name);
        _cookies.Add(new Cookie(name, value, "/"));
    }

    public void RemoveCookie(string name)
    {
        _cookies.RemoveAll(c => c.Name == name);
    }

    public void Dispose() => _client.Dispose();
}
