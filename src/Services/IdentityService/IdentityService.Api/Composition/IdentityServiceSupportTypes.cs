using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Locking;
using His.Hope.Observability;
using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Composition;

// ─── BFF Helpers ─────────────────────────────────────────────────────

internal static class BffHelpers
{
    internal static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? ""));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    internal static string[] ExtractPermissionsFromJwt(string jwt)
    {
        try
        {
            var payload = jwt.Split('.')[1];
            var base64 = payload.Replace('-', '+').Replace('_', '/');
            var padded = base64.PadRight(((base64.Length + 3) / 4) * 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("permissions", out var permProp))
            {
                var value = permProp.GetString();
                if (!string.IsNullOrEmpty(value))
                    return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }
        catch { }
        return [];
    }
}

internal sealed class NoOpLockManager : ILockManager
{
    public Task<IDistributedLock?> AcquireAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default)
        => Task.FromResult<IDistributedLock?>(null);
}

internal sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class => Task.FromResult<T?>(null);
    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default) where T : class => factory();
    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed record PermissionCheckRequest(string? Permission);

// DEPRECATED: Legacy auth endpoints maintained for backward compatibility.
// Migrate to OIDC /connect/authorize and /connect/token.
// These will be removed in Release N+2.
internal static class LegacyEndpointFilter
{
    public static RouteHandlerBuilder WithDeprecationNotice(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (ctx, next) =>
        {
            ctx.HttpContext.Response.Headers["Deprecation"] = "true";
            ctx.HttpContext.Response.Headers["Sunset"] = "Sat, 01 Jan 2028 00:00:00 GMT";
            ctx.HttpContext.Response.Headers["Link"] = "</connect/authorize>; rel=\"successor-version\"";
            return await next(ctx);
        });
    }
}

// Helper: extract userId ("sub" claim) from JWT payload without full validation
internal static class JwtPayloadParser
{
    public static string? ExtractUserIdFromJwtPayload(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;

            var payload = parts[1];
            // Base64Url decode (handle padding)
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sub", out var sub)
                ? sub.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}

// SECURITY: Production configuration validator — runs at startup, fails fast on missing critical config.
internal class ProductionConfigurationValidator
{
    public static void Validate(WebApplication app)
    {
        if (!app.Environment.IsProduction()) return;

        var config = app.Services.GetRequiredService<IConfiguration>();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        var errors = new List<string>();

        // Vault must be configured
        var vaultAddress = config["Vault:Address"];
        if (string.IsNullOrWhiteSpace(vaultAddress) || !vaultAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            errors.Add("Vault:Address is required in production.");
        if (!config.GetValue("Vault:EnableTransit", false))
            errors.Add("Vault:EnableTransit must be true in production.");
        var vaultToken = config["Vault:Token"] ?? config["VAULT_TOKEN"];
        if (string.IsNullOrWhiteSpace(vaultToken) || IsPlaceholder(vaultToken))
            errors.Add("Vault:Token or VAULT_TOKEN is required in production.");

        // Issuer must use HTTPS
        var issuer = config["OpenIddict:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer) || !issuer.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            errors.Add("OpenIddict:Issuer must use HTTPS in production.");

        // Redis must be configured
        var redis = config.GetConnectionString("Redis") ?? config["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redis))
            errors.Add("Redis connection string is required in production.");

        // Insecure HTTP must be disabled
        if (config.GetValue<bool>("OpenIddict:AllowInsecureHttp"))
            errors.Add("OpenIddict:AllowInsecureHttp must be false in production.");

        if (errors.Count > 0)
        {
            foreach (var error in errors)
                logger.LogCritical("Production configuration error: {Error}", error);

            throw new InvalidOperationException(
                $"Production startup aborted — {errors.Count} critical configuration error(s):\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }

        logger.LogInformation("Production configuration validation passed.");
    }

    private static bool IsPlaceholder(string value) =>
        value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}');
}

// Health check for PostgreSQL connectivity
internal class DbHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly string _connectionString;
    public DbHealthCheck(string connectionString) => _connectionString = connectionString;

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            using var conn = new Npgsql.NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(ct);
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("PostgreSQL OK");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("PostgreSQL unavailable", ex);
        }
    }
}

// Health check for Redis connectivity
internal class RedisHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly string _connectionString;
    public RedisHealthCheck(string connectionString) => _connectionString = connectionString;

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            using var redis = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(
                _connectionString, _ => _.ConnectTimeout = 3000);
            var db = redis.GetDatabase();
            await db.PingAsync();
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Redis OK");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Redis unavailable", ex);
        }
    }
}

public partial class Program { }
