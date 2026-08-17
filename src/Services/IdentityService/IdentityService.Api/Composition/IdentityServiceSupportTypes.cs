using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.Infrastructure;
using His.Hope.Contracts.Identity;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Locking;
using His.Hope.Observability;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.Secrets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Composition;

// ─── BFF Helpers ─────────────────────────────────────────────────────

internal static class BffHelpers
{
    internal static string? CookieDomain(IConfiguration configuration)
    {
        var domain = configuration["Authentication:CookieDomain"]?.Trim();
        return string.IsNullOrWhiteSpace(domain) ? null : domain;
    }

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
            ctx.HttpContext.Response.Headers["Link"] = $"<{IdentityApiRoutes.OidcAuthorize}>; rel=\"successor-version\"";
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
        var localHttpMode = config.GetValue("HisHope:LocalHttpMode", false);

        var errors = new List<string>();

        // Vault must be configured
        var vaultAddress = config["Vault:Address"];
        if (string.IsNullOrWhiteSpace(vaultAddress) || !vaultAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            errors.Add("Vault:Address is required in production.");
        if (!config.GetValue("Vault:EnableTransit", false))
            errors.Add("Vault:EnableTransit must be true in production.");
        var vaultRole = config["Vault:Role"];
        var vaultJwtFile = config["Vault:JwtTokenFile"];
        if (string.IsNullOrWhiteSpace(vaultRole) || string.IsNullOrWhiteSpace(vaultJwtFile))
            errors.Add("Vault:Role and Vault:JwtTokenFile are required in production; static Vault tokens are forbidden.");
        if (!string.IsNullOrWhiteSpace(config["Vault:Token"]) || !string.IsNullOrWhiteSpace(config["VAULT_TOKEN"]))
            errors.Add("Vault:Token and VAULT_TOKEN are forbidden in production.");

        // Issuer must use HTTPS
        var issuer = config["OpenIddict:Issuer"];
        if (!localHttpMode && (string.IsNullOrWhiteSpace(issuer) || !issuer.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            errors.Add("OpenIddict:Issuer must use HTTPS in production.");

        // Redis must be configured
        var redis = config.GetConnectionString("Redis") ?? config["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redis))
            errors.Add("Redis connection string is required in production.");

        // Insecure HTTP must be disabled
        if (!localHttpMode && config.GetValue<bool>("OpenIddict:AllowInsecureHttp"))
            errors.Add("OpenIddict:AllowInsecureHttp must be false in production.");

        if (errors.Count > 0)
        {
            foreach (var error in errors)
                logger.LogCritical("Production configuration error: {Error}", error);

            throw new InvalidOperationException(
                $"Production startup aborted — {errors.Count} critical configuration error(s):\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }

        if (localHttpMode)
            logger.LogWarning("Local HTTP compatibility mode is enabled; HTTPS is required before production use.");
        logger.LogInformation("Production configuration validation passed.");
    }

}

// Health check for PostgreSQL connectivity
internal class DbHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly string _connectionString;
    private readonly IVaultDatabaseConnectionStringResolver? _resolver;

    public DbHealthCheck(
        string connectionString,
        IVaultDatabaseConnectionStringResolver? resolver = null)
    {
        _connectionString = connectionString;
        _resolver = resolver;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var connectionString = _resolver?.Resolve(_connectionString, "IdentityDb") ?? _connectionString;
            using var conn = new Npgsql.NpgsqlConnection(connectionString);
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
    private readonly IConfiguration _configuration;

    public RedisHealthCheck(string connectionString, IConfiguration configuration)
    {
        _connectionString = connectionString;
        _configuration = configuration;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var options = RedisConnectionFactory.CreateOptions(_connectionString, _configuration);
            options.ConnectTimeout = 3000;
            using var redis = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(options);
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
