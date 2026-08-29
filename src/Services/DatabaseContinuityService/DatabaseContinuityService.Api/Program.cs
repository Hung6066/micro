using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.Bff.Core.Authentication;
using His.Hope.ServiceDefaults;
using His.Hope.DatabaseContinuityService;
using His.Hope.Authorization;
using His.Hope.Authorization.Requirements;
using His.Hope.SharedKernel.Authorization;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Caching;
using His.Hope.Contracts;
using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHisHopeServiceDefaults(builder.Configuration, "DatabaseContinuityService");
builder.Services.AddHealthChecks().AddCheck(
    "database-continuity-process",
    () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
    tags: ["live", "ready"]);
builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));
builder.Services.AddOptions<DatabaseContinuityOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseContinuityOptions.SectionName))
    .Validate(x => x.RetentionDays is >= 1 and <= 3650, "RetentionDays must be between 1 and 3650")
    .Validate(x => x.KeepLastBackupsPerDatabase is >= 1 and <= 100, "KeepLastBackupsPerDatabase must be between 1 and 100")
    .Validate(x => x.BackupIntervalHours is >= 1 and <= 8760, "BackupIntervalHours must be between 1 and 8760")
    .Validate(x => x.RestoreDrillIntervalHours is >= 1 and <= 8760, "RestoreDrillIntervalHours must be between 1 and 8760")
    .Validate(x => x.MaxAttempts is >= 1 and <= 10, "MaxAttempts must be between 1 and 10")
    .ValidateOnStart();
builder.Services.AddSingleton<VaultContinuityClient>();
builder.Services.AddSingleton<ContinuityAuditStore>();
builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var endpoint = builder.Configuration["AWS_ENDPOINT_URL"] ?? builder.Configuration["AWS_S3_ENDPOINT"];
    var region = builder.Configuration["AWS_REGION"] ?? builder.Configuration["AWS_DEFAULT_REGION"] ?? "us-east-1";
    var s3Config = new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(region) };
    if (!string.IsNullOrWhiteSpace(endpoint))
    {
        s3Config.ServiceURL = endpoint;
        s3Config.ForcePathStyle = true;
    }
    return new AmazonS3Client(s3Config);
});
builder.Services.AddSingleton<IBackupStorageProvider, LocalBackupStorageProvider>();
builder.Services.AddSingleton<IBackupStorageProvider, S3CompatibleBackupStorageProvider>();
builder.Services.AddSingleton<BackupStorageCoordinator>();
var sessionRedis = RedisConnectionFactory.Connect(
    builder.Configuration.GetConnectionString("Redis")
        ?? builder.Configuration["Redis:ConnectionString"]
        ?? "localhost:6379",
    builder.Configuration);
builder.Services.AddSingleton<IConnectionMultiplexer>(sessionRedis);
builder.Services.AddDataProtection()
    .SetApplicationName("His.Hope.IdentityService")
    .PersistKeysToStackExchangeRedis(
        sessionRedis,
        builder.Configuration["DataProtection:KeyName"]
            ?? "HisHope:IdentityService:DataProtection:Keys");
builder.Services.AddSingleton<SessionTokenProtector>();
builder.Services.AddSingleton<ContinuityJobStore>();
builder.Services.AddSingleton<ContinuityExecutor>();
builder.Services.AddHostedService<ContinuityScheduler>();
builder.Services.AddHostedService<ContinuityWorker>();
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);
builder.Services.AddHisHopeAuthorization();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Continuity.Write", policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(
            new PermissionRequirement(HisHopePermissions.Admin.SettingsWrite),
            // Backup/restore changes are workload/operator operations. A
            // human admin permission without the explicit service scope is
            // intentionally insufficient.
            new ScopeRequirement("platform.continuity.write"),
            // Both interactive operators and service workers are valid, but
            // each must carry an explicit IdentityService principal type.
            new PrincipalTypeRequirement(AuthorizationConstants.PrincipalTypes.Human, AuthorizationConstants.PrincipalTypes.Workload)));
builder.Services.AddHisHopeDpopValidation();

var app = builder.Build();
await app.Services.GetRequiredService<ContinuityAuditStore>().EnsureSchemaAsync(CancellationToken.None);
// Keep the process probe independent from the optional continuity dependencies.
// This endpoint must remain available while the worker is waiting for a database
// or Vault to become reachable.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/health")
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync("ok");
        return;
    }

    await next();
});
app.UseHisHopeServiceDefaults();
// Reuse the shared BFF session contract for direct admin service calls. The
// gateway carries hishop_sid and this bridge turns its protected session JWT
// into the standard bearer token before service authorization runs.
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("Authorization"))
    {
        var redis = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
        var tokenProtector = context.RequestServices.GetRequiredService<SessionTokenProtector>();
        var protectedJwt = context.Request.Headers["X-HisHope-Session-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(protectedJwt) &&
            context.Request.Cookies.TryGetValue("hishop_sid", out var sessionId) &&
            !string.IsNullOrWhiteSpace(sessionId))
        {
            var sessionJson = await redis.GetDatabase().StringGetAsync($"session:{sessionId}");
            if (sessionJson.HasValue)
            {
                using var sessionDocument = JsonDocument.Parse((string)sessionJson!);
                protectedJwt = sessionDocument.RootElement.TryGetProperty("Jwt", out var jwtElement)
                    ? jwtElement.GetString()
                    : null;
            }
        }
        if (!string.IsNullOrWhiteSpace(protectedJwt))
        {
            try { context.Request.Headers.Authorization = $"Bearer {tokenProtector.Unprotect(protectedJwt)}"; }
            catch (System.Security.Cryptography.CryptographicException)
            { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
        }
    }
    await next();
});
app.UseAuthentication();
// Browser admin-app sessions are issued by Identity as a protected BFF
// session. Most services consume the nested JWT directly, but continuity is
// also reached through the gateway while the browser intentionally keeps the
// access token server-side. If JWT validation cannot select the nested-token
// decryption key during a local key-ring rotation, recover the authenticated
// principal from the same protected session record instead of returning a
// misleading 401. The DataProtection payload and Redis TTL remain the trust
// boundary; permissions are never accepted from an unprotected request.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated != true &&
        context.Request.Cookies.TryGetValue("hishop_sid", out var sessionId) &&
        !string.IsNullOrWhiteSpace(sessionId))
    {
        var sessionJson = await sessionRedis.GetDatabase().StringGetAsync($"session:{sessionId}");
        if (sessionJson.HasValue)
        {
            try
            {
                using var document = JsonDocument.Parse((string)sessionJson!);
                var root = document.RootElement;
                var protectedJwt = root.TryGetProperty("Jwt", out var jwtElement) ? jwtElement.GetString() : null;
                var expiresAt = root.TryGetProperty("ExpiresAt", out var expiryElement)
                    ? expiryElement.GetDateTimeOffset()
                    : DateTimeOffset.MinValue;
                if (!string.IsNullOrWhiteSpace(protectedJwt) && expiresAt > DateTimeOffset.UtcNow)
                {
                    // Unprotect is an integrity/authenticity check against the
                    // shared Identity key ring; the nested JWT itself remains
                    // available for downstream services that need it.
                    _ = context.RequestServices.GetRequiredService<SessionTokenProtector>().Unprotect(protectedJwt);
                    var claims = new List<Claim>();
                    if (root.TryGetProperty("UserId", out var userIdElement) && userIdElement.GetString() is { } userId)
                    {
                        claims.Add(new Claim("sub", userId));
                        claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
                    }
                    var principalType = root.TryGetProperty("PrincipalType", out var principalTypeElement)
                        ? principalTypeElement.GetString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(principalType))
                        claims.Add(new Claim(AuthorizationConstants.Claims.PrincipalType, principalType));
                    if (root.TryGetProperty("Permissions", out var permissionsElement) && permissionsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var permission in permissionsElement.EnumerateArray())
                        {
                            if (permission.GetString() is { Length: > 0 } value)
                                claims.Add(new Claim("permissions", value));
                        }
                    }
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "HisHopeSession"));
                }
            }
            catch (CryptographicException)
            {
                // Fail closed; the normal JWT challenge remains authoritative.
            }
            catch (JsonException)
            {
                // Fail closed on malformed session records.
            }
        }
    }
    await next();
});
app.UseDpopAuthorizationSchemeNormalization();
app.UseDpopAccessTokenValidation();
app.UseAuthorization();
app.UseHisHopeTenantScope();
app.MapHisHopeHealthEndpoints();

// Keep continuity APIs on the same permission contract as the other admin APIs.
// Browser BFF sessions carry permission claims; a role-only policy rejects a
// valid admin session at this service boundary.
var admin = app.MapGroup("/api/v1/admin/database-continuity").RequireAuthorization();
admin.MapGet("/status", async (IOptions<DatabaseContinuityOptions> config, ContinuityJobStore store, VaultContinuityClient vault, CancellationToken ct) =>
{
    var value = config.Value;
    var latest = await store.GetLatestAsync(ct);
    var lastSuccessfulBackupAt = await store.GetLastCompletedAtAsync("backup");
    var lastSuccessfulRestoreDrillAt = await store.GetLastCompletedAtAsync("restore-drill");
    var vaultStatus = await vault.GetStatusAsync(ct);
    var vaultRequired = value.EncryptionProvider.Contains("vault", StringComparison.OrdinalIgnoreCase);
    var configurationErrors = GetConfigurationErrors(value, vaultRequired, vaultStatus);
    var ready = value.Enabled && configurationErrors.Count == 0;
    var alerts = new List<string>(configurationErrors);
    if (vaultRequired && !vaultStatus.Reachable) alerts.Add("vault_unavailable");
    if (latest?.Status == ContinuityJobStatus.Failed) alerts.Add("latest_job_failed");
    return Results.Ok(new
    {
        enabled = value.Enabled,
        schedulerEnabled = value.SchedulerEnabled,
        backupIntervalHours = value.BackupIntervalHours,
        provider = value.Provider,
        storageUri = RedactStorageUri(value.StorageUri),
        storageProvider = value.StorageProvider,
        storageFallbackEnabled = value.StorageFallbackEnabled,
        encryptionProvider = value.EncryptionProvider,
        encryption = new { provider = value.EncryptionProvider, configured = !string.IsNullOrWhiteSpace(value.EncryptionProvider) },
        retentionDays = value.RetentionDays,
        keepLastBackupsPerDatabase = value.KeepLastBackupsPerDatabase,
        pitrEnabled = value.PitrEnabled,
        targetRpoMinutes = value.TargetRpoMinutes,
        targetRtoMinutes = value.TargetRtoMinutes,
        restoreDrillIntervalHours = value.RestoreDrillIntervalHours,
        maxAttempts = value.MaxAttempts,
        lastSuccessfulBackupAt,
        lastSuccessfulRestoreDrillAt,
        executorConfigured = !string.IsNullOrWhiteSpace(value.ExecutorPath),
        ready,
        configurationErrors = configurationErrors.ToArray(),
        alerts = alerts.ToArray(),
        vault = vaultStatus,
        latestJob = latest
    });
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);
app.MapGet("/metrics", async (ContinuityJobStore store, VaultContinuityClient vault, CancellationToken ct) =>
{
    var latest = await store.GetLatestAsync(ct);
    var lastSuccessfulBackupAt = await store.GetLastCompletedAtAsync("backup");
    var lastSuccessfulRestoreDrillAt = await store.GetLastCompletedAtAsync("restore-drill");
    var vaultStatus = await vault.GetStatusAsync(ct);
    var lines = new List<string>
    {
        "# HELP his_hope_database_continuity_vault_available Vault Transit availability (1/0).",
        "# TYPE his_hope_database_continuity_vault_available gauge",
        $"his_hope_database_continuity_vault_available {(vaultStatus.Reachable && vaultStatus.KeyVersion is not null ? 1 : 0)}",
        "# HELP his_hope_database_continuity_vault_key_version Latest Vault Transit key version.",
        "# TYPE his_hope_database_continuity_vault_key_version gauge",
        $"his_hope_database_continuity_vault_key_version {vaultStatus.KeyVersion ?? 0}",
        "# HELP his_hope_database_continuity_pitr_enabled PITR archive configuration (1/0).",
        "# TYPE his_hope_database_continuity_pitr_enabled gauge",
        $"his_hope_database_continuity_pitr_enabled {(builder.Configuration["DatabaseContinuity:PitrEnabled"] == "true" ? 1 : 0)}",
        "# HELP his_hope_database_continuity_last_success_timestamp_seconds Last successful job timestamp by operation.",
        "# TYPE his_hope_database_continuity_last_success_timestamp_seconds gauge",
        $"his_hope_database_continuity_last_success_timestamp_seconds{{operation=\"backup\"}} {lastSuccessfulBackupAt?.ToUnixTimeSeconds() ?? 0}",
        $"his_hope_database_continuity_last_success_timestamp_seconds{{operation=\"restore-drill\"}} {lastSuccessfulRestoreDrillAt?.ToUnixTimeSeconds() ?? 0}",
        "# HELP his_hope_database_continuity_latest_job_status Latest continuity job status (queued=1,running=2,completed=3,failed=4).",
        "# TYPE his_hope_database_continuity_latest_job_status gauge",
        $"his_hope_database_continuity_latest_job_status {(latest?.Status switch { ContinuityJobStatus.Queued => 1, ContinuityJobStatus.Running => 2, ContinuityJobStatus.Completed => 3, ContinuityJobStatus.Failed => 4, _ => 0 })}",
    };
    if (latest?.ResultJson is not null)
    {
        try
        {
            using var result = System.Text.Json.JsonDocument.Parse(latest.ResultJson);
            if (result.RootElement.TryGetProperty("durationSeconds", out var duration))
                lines.Add($"his_hope_database_continuity_last_job_duration_seconds {duration.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
        catch (System.Text.Json.JsonException) { }
    }
    return Results.Text(string.Join('\n', lines) + "\n", "text/plain; version=0.0.4");
}).AllowAnonymous();
admin.MapPost("/backups", async (HttpContext http, ContinuityJobStore store, VaultContinuityClient vault, IOptions<DatabaseContinuityOptions> config, CancellationToken ct) =>
{
    var value = config.Value;
    if (!IsReady(value, await vault.GetStatusAsync(ct)))
        return Results.Problem(statusCode: 503, extensions: new Dictionary<string, object?>
        {
            [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.Internal
        });
    var job = new ContinuityJob
    {
        Operation = "backup",
        TargetEnvironment = "production",
        ActorSubject = http.User.FindFirst("sub")?.Value ?? "admin",
        CorrelationId = http.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? http.TraceIdentifier
    };
    await store.EnqueueAsync(job, ct);
    return Results.Accepted($"/api/v1/admin/database-continuity/jobs/{job.JobId}", job);
}).RequireAuthorization("Continuity.Write");
admin.MapPost("/restore-drills", async (HttpContext http, ContinuityJobStore store, VaultContinuityClient vault, IOptions<DatabaseContinuityOptions> config, CancellationToken ct) =>
{
    var value = config.Value;
    if (!IsReady(value, await vault.GetStatusAsync(ct)))
        return Results.Problem(statusCode: 503, extensions: new Dictionary<string, object?>
        {
            [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.Internal
        });
    var job = new ContinuityJob
    {
        Operation = "restore-drill",
        TargetEnvironment = value.RestoreDrillTargetEnvironment,
        RestorePoint = DateTimeOffset.UtcNow,
        ActorSubject = http.User.FindFirst("sub")?.Value ?? "admin",
        CorrelationId = http.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? http.TraceIdentifier
    };
    await store.EnqueueAsync(job, ct);
    return Results.Accepted($"/api/v1/admin/database-continuity/jobs/{job.JobId}", job);
}).RequireAuthorization("Continuity.Write");
admin.MapGet("/jobs/{jobId}", async (string jobId, ContinuityJobStore store, CancellationToken ct) =>
{
    var job = await store.GetAsync(jobId, ct);
    return job is null ? Results.NotFound() : Results.Ok(job);
}).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);
admin.MapGet("/audit", async (int page, int pageSize, ContinuityAuditStore audit, CancellationToken ct) =>
    Results.Ok(await audit.ListAsync(page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, ct)))
    .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminAuditRead);

app.Run();

static string? RedactStorageUri(string value) =>
    Uri.TryCreate(value, UriKind.Absolute, out var uri) ? $"{uri.Scheme}://{uri.Host}/…" :
    string.IsNullOrWhiteSpace(value) ? null : "configured";

static bool IsReady(DatabaseContinuityOptions value, VaultContinuityStatus vault) =>
    value.Enabled && GetConfigurationErrors(value, value.EncryptionProvider.Contains("vault", StringComparison.OrdinalIgnoreCase), vault).Count == 0;

static List<string> GetConfigurationErrors(DatabaseContinuityOptions value, bool vaultRequired, VaultContinuityStatus vault)
{
    var errors = new List<string>();
    if (!value.Enabled) errors.Add("continuity_disabled");
    if (string.IsNullOrWhiteSpace(value.ExecutorPath)) errors.Add("executor_not_configured");
    else if (!Path.IsPathFullyQualified(value.ExecutorPath)) errors.Add("executor_path_must_be_absolute");
    if (string.IsNullOrWhiteSpace(value.StorageUri)) errors.Add("storage_not_configured");
    if (vaultRequired && vault is not { Reachable: true, KeyVersion: not null }) errors.Add("vault_unavailable");
    return errors;
}

public partial class Program { }
