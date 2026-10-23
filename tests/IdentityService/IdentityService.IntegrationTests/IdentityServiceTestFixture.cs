using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Threading.Channels;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.Infrastructure.Audit;
using His.Hope.ServiceDefaults;
using His.Hope.IdentityService.Testing;
using His.Hope.Contracts.Identity;
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
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public class IdentityServiceTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private string? _postgresConnectionString;
    private RedisContainer? _redis;
    private WebApplication? _app;
    private string? _keyDirectory;

    public HttpClient AnonymousClient { get; private set; } = null!;
    public IServiceProvider Services => _app!.Services;
    public string PostgresConnectionString => _postgresConnectionString ?? "";

    public async Task InitializeAsync()
    {
        var configuredPostgres = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredPostgres))
        {
            _postgres = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("hishopetest")
                .WithUsername("testuser")
                .WithPassword("testpass123!")
                .WithCleanUp(true)
                .Build();

            await _postgres.StartAsync();
            _postgresConnectionString = GetPostgresConnectionString(_postgres);
        }
        else
        {
            // Windows Docker Desktop can intermittently lose Testcontainers' random
            // host-port forwarding during a long suite. CI/local callers may opt into
            // a dedicated, already-running PostgreSQL database without changing the
            // production wiring or default isolated-container behavior.
            _postgresConnectionString = configuredPostgres;
        }

        var pgConnStr = _postgresConnectionString;
        var configuredRedis = Environment.GetEnvironmentVariable("IDENTITY_TEST_REDIS_CONNECTION");
        string redisConnStr;
        if (!string.IsNullOrWhiteSpace(configuredRedis))
        {
            // Allows local Docker Compose/CI to provide an already-running
            // isolated Redis and avoids flaky Windows host-port forwarding.
            redisConnStr = configuredRedis;
        }
        else
        {
            _redis = new RedisBuilder("redis:7-alpine")
                .WithCleanUp(true)
                .Build();
            await _redis.StartAsync();
            redisConnStr = GetRedisConnectionString(_redis);
        }
        using var signingRsa = RSA.Create(2048);
        using var encryptionRsa = RSA.Create(2048);
        _keyDirectory = Path.Combine(Path.GetTempPath(), "hishop-identity-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_keyDirectory);
        var signingKeyPath = Path.Combine(_keyDirectory, "jwt-signing-private.pem");
        var signingPublicKeyPath = Path.Combine(_keyDirectory, "jwt-signing-public.pem");
        var encryptionKeyPath = Path.Combine(_keyDirectory, "jwt-encryption-private.pem");
        await File.WriteAllTextAsync(signingKeyPath, signingRsa.ExportRSAPrivateKeyPem());
        await File.WriteAllTextAsync(signingPublicKeyPath, signingRsa.ExportRSAPublicKeyPem());
        await File.WriteAllTextAsync(encryptionKeyPath, encryptionRsa.ExportRSAPrivateKeyPem());

        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                await using var redis = await ConnectionMultiplexer.ConnectAsync(redisConnStr);
                if (redis.IsConnected)
                    break;
            }
            catch (RedisConnectionException) when (attempt < 20)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt));
            }
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

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
            ["Jwt:AllowHttp"] = "true",
            ["Jwt:Key"] = "integration-test-signing-key-32-bytes-long!",
            ["Jwt:RsaPrivateKeyPath"] = signingKeyPath,
            ["Jwt:RsaPublicKeyPath"] = signingPublicKeyPath,
            ["Jwt:RsaEncryptionPrivateKeyPath"] = encryptionKeyPath,
            // Match the production-shaped auth bucket; individual session
            // clients and explicit rate-limit tests use isolated keys.
            ["RateLimiting:AuthPermitLimit"] = "120",
            // The full integration suite intentionally exercises hundreds of
            // endpoints through one TestServer IP. Keep abuse-limit behavior
            // covered by dedicated rate-limit tests, while preventing the
            // aggregate suite from exhausting a shared infrastructure bucket.
            ["RateLimiting:MaxRequestsPerIp"] = "10000",
            ["RateLimiting:MaxRequestsPerUser"] = "5000",
            // Tests deliberately send isolated X-RateLimit-Key values. This
            // switch is scoped to the in-memory Testing configuration only;
            // production keeps the forwarded-key trust default disabled.
            ["RateLimiting:TrustForwardedKey"] = "true",
            ["Vault:EnableTransit"] = "false",
            ["Vault:RequireVault"] = "false",
            ["Identity:BootstrapAdmin:Password"] = IdentityTestData.DefaultPassword
            , ["Authentication:Google:ClientId"] = "integration-google-client"
            , ["Authentication:Google:ClientSecret"] = "integration-google-secret"
            , ["Authentication:LegacyAuthSunset"] = "Sat, 01 Jan 2028 00:00:00 GMT"
            , ["Assurance:PolicyPath"] = ResolveAssurancePolicyPath()
            // This fixture models the legacy single-tenant API. Conglomerate
            // isolation is covered by dedicated registry/authorization tests;
            // keep endpoint tests independent from host appsettings.
            , ["Conglomerate:Enabled"] = "false"
        });
        builder.AddIdentityService();
        // AddIdentityService loads the API appsettings after the test overlay;
        // force the isolated fixture back to its no-external-Vault contract.
        builder.Configuration["Vault:Address"] = "";
        builder.Configuration["Vault:RequireVault"] = "false";
        // Re-apply test-only rate-limit isolation after the API registration
        // loads appsettings.json. Production keeps forwarded-key trust off;
        // integration tests use a unique key per independent flow so the
        // aggregate suite does not exhaust one shared TestServer IP bucket.
        builder.Configuration["RateLimiting:TrustForwardedKey"] = "true";
        builder.Configuration["RateLimiting:MaxRequestsPerIp"] = "10000";
        builder.Configuration["RateLimiting:MaxRequestsPerUser"] = "5000";
        builder.Configuration["Conglomerate:Enabled"] = "false";
        builder.Configuration["Identity:SuperAdmin:UserIds:0"] = IdentityTestData.AdminId.ToString("D");
        builder.Configuration["Authentication:OidcClients:his-hope-test:RedirectUris:0"] = "http://localhost:4200/auth/callback";
        builder.Configuration["Authentication:OidcClients:his-hope-test:PostLogoutRedirectUris:0"] = "http://localhost:4200/auth/login";

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
            await EnsureDatabaseCreatedWithRetryAsync(db);

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

            // The API intentionally skips production migration/seed during tests.
            // Seed the canonical permission catalog and grant the test Admin role
            // the same unrestricted permission set as IdentityDbInitializer so
            // authenticated endpoint tests exercise authorization, not a hollow
            // role with zero permissions.
            var adminRole = await roleManager.FindByNameAsync("Admin")
                ?? throw new InvalidOperationException("Test Admin role was not created.");
            foreach (var descriptor in IdentityTestData.CanonicalPermissions())
            {
                if (!await db.Permissions.AnyAsync(permission => permission.Code == descriptor.Code))
                {
                    db.Permissions.Add(new Permission
                    {
                        Code = descriptor.Code,
                        Name = descriptor.Name,
                        Group = descriptor.Group,
                        Description = descriptor.Description,
                        IsSystem = true
                    });
                }

                if (!await db.RolePermissions.AnyAsync(rolePermission =>
                        rolePermission.RoleId == adminRole.Id && rolePermission.PermissionCode == descriptor.Code))
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = adminRole.Id,
                        PermissionCode = descriptor.Code
                    });
                }
            }
            await db.SaveChangesAsync();

            var adminUser = await userManager.FindByNameAsync(IdentityTestData.AdminUserName);
            if (adminUser is null)
            {
                adminUser = new User
                {
                    Id = IdentityTestData.AdminId,
                    UserName = IdentityTestData.AdminUserName,
                    Email = IdentityTestData.AdminEmail,
                    FirstName = "Test",
                    LastName = "Admin",
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(adminUser, IdentityTestData.DefaultPassword);
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Could not create integration admin: {string.Join(';', result.Errors.Select(error => error.Description))}");
            }

            // Initializers may have created the bootstrap admin before this
            // fixture runs. Always repair its test invariants so every test
            // gets the same authenticated permission surface.
            if (!adminUser.IsActive)
            {
                adminUser.IsActive = true;
                await userManager.UpdateAsync(adminUser);
            }

            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                await userManager.AddToRoleAsync(adminUser, "Admin");

            // Tenant-aware admin endpoints require an explicit HQ membership;
            // production bootstrap supplies this claim, while integration
            // tests intentionally skip the production initializer.
            var adminClaims = await userManager.GetClaimsAsync(adminUser);
            if (!adminClaims.Any(claim => claim.Type == "tenant_membership" &&
                string.Equals(claim.Value, "group-hq", StringComparison.OrdinalIgnoreCase)))
            {
                await userManager.AddClaimAsync(adminUser, new Claim("tenant_membership", "group-hq"));
            }

            // Keep the bootstrap invariant explicit even when a pre-existing
            // database contains a stale Identity role mapping. This makes the
            // fixture deterministic across reused PostgreSQL volumes.
            if (!await db.UserRoles.AnyAsync(link => link.UserId == adminUser.Id && link.RoleId == adminRole.Id))
            {
                db.UserRoles.Add(new IdentityUserRole<Guid>
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id
                });
                await db.SaveChangesAsync();
            }

            var seededRoleNames = await userManager.GetRolesAsync(adminUser);
            var seededPermissionCount = await db.RolePermissions
                .CountAsync(link => link.RoleId == adminRole.Id);
            if (!seededRoleNames.Contains("Admin", StringComparer.OrdinalIgnoreCase) || seededPermissionCount == 0)
            {
                throw new InvalidOperationException(
                    $"Identity test bootstrap invariant failed: roles=[{string.Join(',', seededRoleNames)}], permissions={seededPermissionCount}.");
            }
        }

        await _app.StartAsync();
        await WaitForRedisAsync(_app.Services);
        AnonymousClient = _app.GetTestClient();
    }

    private static bool UseContainerNetworkAddresses =>
        string.Equals(Environment.GetEnvironmentVariable("IDENTITY_TEST_USE_CONTAINER_IP"), "true", StringComparison.OrdinalIgnoreCase);

    private static string GetPostgresConnectionString(PostgreSqlContainer container)
    {
        var connectionString = container.GetConnectionString();
        if (!UseContainerNetworkAddresses)
            return connectionString;

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Host = container.IpAddress,
            Port = 5432
        };
        return builder.ConnectionString;
    }

    private static string GetRedisConnectionString(RedisContainer container) =>
        UseContainerNetworkAddresses ? $"{container.IpAddress}:6379" : container.GetConnectionString();

    private static async Task WaitForRedisAsync(IServiceProvider services)
    {
        var redis = services.GetRequiredService<IConnectionMultiplexer>();
        Exception? last = null;
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                await redis.GetDatabase().PingAsync();
                return;
            }
            catch (Exception ex) when (ex is RedisException or TimeoutException)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(500 * attempt, 3000)));
            }
        }

        throw new TimeoutException("Redis test connection could not execute PING after retries.", last);
    }

    private static async Task EnsureDatabaseCreatedWithRetryAsync(IdentityDbContext db)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            try
            {
                // Apply the complete migration chain so provisioned Docker
                // databases exercise the same schema as the running service.
                await db.Database.MigrateAsync();
                return;
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(500 * attempt, 3000)));
            }
        }

        throw new TimeoutException("PostgreSQL test database could not be created after retries.", last);
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
        if (_keyDirectory is not null && Directory.Exists(_keyDirectory))
            Directory.Delete(_keyDirectory, recursive: true);
    }

    public SessionClient CreateSessionClient()
    {
        var client = _app!.GetTestClient();
        return new SessionClient(client);
    }

    public async Task<SessionClient> CreateAuthenticatedSessionAsync()
    {
        var session = CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestData.AdminEmail, IdentityTestData.DefaultPassword);
        if (!response.IsSuccessStatusCode)
        {
            // Registration fallback
            var regResponse = await session.InnerClient.PostAsJsonAsync(IdentityApiRoutes.Auth + "/register",
                new { email = "test-user@test.test", password = IdentityTestData.DefaultPassword, firstName = "Test", lastName = "User" });
            if (regResponse.IsSuccessStatusCode)
                await session.LoginAsync("test-user@test.test", IdentityTestCredentials.Password);
        }
        else
        {
            // Mutation integration tests model the real step-up contract with a
            // freshly issued MFA-assured bearer token; production guards remain enabled.
            session.SetBearerToken(await CreateFreshMfaAdminTokenAsync());
        }
        return session;
    }

    public async Task<string> CreateFreshMfaAdminTokenAsync()
    {
        await using var scope = _app!.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<JwtTokenGenerator>();
        var user = await userManager.FindByEmailAsync(IdentityTestData.AdminEmail)
            ?? throw new InvalidOperationException("Integration admin was not seeded.");
        var roles = await userManager.GetRolesAsync(user);
        var roleIds = await db.Roles
            .Where(role => roles.Contains(role.Name!))
            .Select(role => role.Id)
            .ToListAsync();
        var permissions = await db.RolePermissions
            .Where(link => roleIds.Contains(link.RoleId))
            .Select(link => link.PermissionCode)
            .Distinct()
            .ToListAsync();
        var userClaims = await userManager.GetClaimsAsync(user);
        var additionalClaims = userClaims
            .Where(claim => claim.Type is "tenant_membership" or "tenant_id" or "tenant_memberships" or "facility_id" or "cross_facility" or "portal_class")
            .Concat(userClaims
                .Where(claim => claim.Type == "tenant_membership")
                .Select((claim, index) => index == 0 ? new Claim("tenant_id", claim.Value) : null)
                .Where(claim => claim is not null)!
                .Cast<Claim>())
            .Append(new Claim("portal_class", His.Hope.IdentityService.Application.Conglomerate.ConglomerateConstants.PortalClassOperator))
            .Append(new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var token = tokenGenerator.GenerateAccessToken(user, roles, permissions, ["pwd", "mfa"], additionalClaims).token;
        var principal = tokenGenerator.GetPrincipalFromExpiredToken(token)
            ?? throw new InvalidOperationException("Fresh MFA test token contract failed: token could not be parsed.");
        if (!StepUpAuthenticationGuard.HasFreshMfa(principal))
            throw new InvalidOperationException($"Fresh MFA test token contract failed: auth claims=[{string.Join(';', principal.Claims.Where(claim => claim.Type.Contains("amr", StringComparison.OrdinalIgnoreCase) || claim.Type.Contains("authentication", StringComparison.OrdinalIgnoreCase)).Select(claim => $"{claim.Type}={claim.Value}"))}]");
        if (!principal.FindAll("tenant_membership").Any())
            throw new InvalidOperationException("Fresh MFA test token contract failed: tenant membership claim is missing.");
        if (!string.Equals(principal.FindFirst("portal_class")?.Value, "operator", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Fresh MFA test token contract failed: portal class=[{string.Join(';', principal.FindAll("portal_class").Select(claim => claim.Value))}]");
        if (!principal.FindAll("super_admin").Any(claim => string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Fresh MFA test token contract failed: super_admin claim is missing.");
        return token;
    }

    private static string ResolveAssurancePolicyPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 10 && current is not null; depth++, current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "config", "assurance-policy.v1.json");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException("config/assurance-policy.v1.json was not found for integration tests.");
    }
}

[CollectionDefinition("IdentityServiceIntegration", DisableParallelization = true)]
public class IdentityServiceIntegrationCollection : ICollectionFixture<IdentityServiceTestFixture>
{
}

public class SessionClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly List<Cookie> _cookies = new();
    private readonly string _authRateLimitKey = $"session-auth-{Guid.NewGuid():N}";
    private string? _bearerToken;

    public HttpClient InnerClient => _client;
    public string? RateLimitKey { get; set; }
    public SessionClient(HttpClient client) => _client = client;
    public void SetBearerToken(string token) => _bearerToken = token;

    public Task<HttpResponseMessage> LoginAsAdminAsync() =>
        LoginAsync(IdentityTestData.AdminEmail, IdentityTestData.DefaultPassword);

    public async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, IdentityApiRoutes.Login)
        {
            Content = JsonContent.Create(new { email, password })
        };
        request.Headers.Add("X-RateLimit-Key", _authRateLimitKey);
        var response = await _client.SendAsync(request);
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
        ApplyBearer(request);
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
        ApplyBearer(request);
        foreach (var cookie in _cookies)
            request.Headers.TryAddWithoutValidation("Cookie", $"{cookie.Name}={cookie.Value}");
        var csrf = GetCookieValue("hishop_csrf");
        if (csrf is not null)
            request.Headers.Add("X-CSRF-Token", csrf);
        if (RateLimitKey is not null)
            request.Headers.Add("X-RateLimit-Key", RateLimitKey);
        return await _client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> PutWithCookiesAsync(string url, object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        ApplyBearer(request);
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

    public async Task<HttpResponseMessage> DeleteWithCookiesAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        ApplyBearer(request);
        foreach (var cookie in _cookies)
            request.Headers.TryAddWithoutValidation("Cookie", $"{cookie.Name}={cookie.Value}");
        var csrf = GetCookieValue("hishop_csrf");
        if (csrf is not null)
            request.Headers.Add("X-CSRF-Token", csrf);
        if (RateLimitKey is not null)
            request.Headers.Add("X-RateLimit-Key", RateLimitKey);
        return await _client.SendAsync(request);
    }

    private void ApplyBearer(HttpRequestMessage request)
    {
        if (_bearerToken is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);
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
