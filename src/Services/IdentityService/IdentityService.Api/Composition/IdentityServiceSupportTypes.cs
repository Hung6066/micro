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

}

internal sealed class NoOpLockManager : ILockManager
{
    public Task<IDistributedLock?> AcquireAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default)
        => Task.FromResult<IDistributedLock?>(null);
}

internal sealed record PermissionCheckRequest(string? Permission);
internal sealed record UpdateLanguagePreferenceRequest(string PreferredLanguage);

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
