using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class HrWebhookEndpoints
{
    public static RouteGroupBuilder MapHrWebhookEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/webhook/hr", ProcessHrWebhook);
        return group;
    }

    private static async Task<Results<Ok<HrWebhookResponse>, ProblemHttpResult>> ProcessHrWebhook(
        HttpContext httpContext,
        IConfiguration configuration,
        BulkUserImportService importService,
        CancellationToken ct)
    {
        using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(ct);

        IHrWebhookReplayStore replayStore;
        if (httpContext.RequestServices.GetService<IConnectionMultiplexer>() is { } redis)
        {
            replayStore = new RedisHrWebhookReplayStore(redis);
        }
        else if (httpContext.RequestServices.GetService<IDistributedCache>() is { } cache)
        {
            replayStore = new DistributedCacheHrWebhookReplayStore(cache);
        }
        else
        {
            replayStore = BoundedInMemoryHrWebhookReplayStore.Shared;
        }

        var authResult = await HrWebhookAuthenticator.AuthenticateAsync(
            httpContext.Request,
            rawBody,
            configuration,
            replayStore,
            ct);

        if (!authResult.Succeeded)
            return TypedResults.Problem(authResult.Detail, statusCode: authResult.StatusCode);

        HrWebhookEvent? hrEvent;
        try
        {
            hrEvent = JsonSerializer.Deserialize<HrWebhookEvent>(
                rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return TypedResults.Problem("Invalid HR webhook JSON payload.", statusCode: 400);
        }

        if (hrEvent is null)
            return TypedResults.Problem("Invalid HR webhook JSON payload.", statusCode: 400);

        if (string.IsNullOrWhiteSpace(hrEvent.EventType) || hrEvent.Employee is null)
            return TypedResults.Problem("Invalid HR webhook event payload.", statusCode: 400);

        if (!string.IsNullOrWhiteSpace(hrEvent.EventId) &&
            !string.Equals(hrEvent.EventId, authResult.EventId, StringComparison.Ordinal))
        {
            return TypedResults.Problem("HR webhook event id mismatch.", statusCode: 400);
        }

        switch (hrEvent.EventType.ToLowerInvariant())
        {
            case "employee.hired":
            case "employee.updated":
                var record = new BulkUserRecord(
                    UserName: hrEvent.Employee.Email ?? hrEvent.Employee.EmployeeId ?? "",
                    Email: hrEvent.Employee.Email ?? "",
                    FirstName: hrEvent.Employee.FirstName ?? "",
                    LastName: hrEvent.Employee.LastName ?? "",
                    MiddleName: null,
                    LicenseNumber: hrEvent.Employee.LicenseNumber,
                    Specialty: hrEvent.Employee.Department,
                    Role: MapDepartmentToRole(hrEvent.Employee.Department),
                    FacilityId: hrEvent.Employee.FacilityId,
                    IsActive: hrEvent.EventType == "employee.hired"
                );

                var importRequest = new BulkImportRequest(
                    new List<BulkUserRecord> { record },
                    SkipExisting: false
                );

                var result = await importService.ImportAsync(importRequest, ct);
                return TypedResults.Ok(new HrWebhookResponse(
                    result.Succeeded > 0 ? "provisioned" : "error",
                    hrEvent.Employee.EmployeeId ?? "",
                    result.Errors.FirstOrDefault()?.Error));

            case "employee.terminated":
                return TypedResults.Ok(new HrWebhookResponse("acknowledged",
                    hrEvent.Employee.EmployeeId ?? "", "User deactivation handled via SCIM PATCH"));

            default:
                return TypedResults.Problem($"Unsupported event type: {hrEvent.EventType}", statusCode: 400);
        }
    }

    private static string? MapDepartmentToRole(string? department)
    {
        return department?.ToLowerInvariant() switch
        {
            "nursing" => "Nurse",
            "laboratory" => "LabTechnician",
            "pharmacy" => "Pharmacist",
            "billing" => "BillingClerk",
            "reception" => "Receptionist",
            "medical" => "Provider",
            _ => "Provider"
        };
    }
}

public record HrWebhookEvent(
    string EventType,
    string EventId,
    DateTime Timestamp,
    HrEmployee Employee);

public record HrEmployee(
    string? EmployeeId,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Department,
    string? LicenseNumber,
    string? FacilityId);

public record HrWebhookResponse(string Status, string EmployeeId, string? Error);

public static class HrWebhookAuthenticator
{
    public const string TimestampHeader = "X-HisHope-Timestamp";
    public const string SignatureHeader = "X-HisHope-Signature";
    public const string EventIdHeader = "X-HisHope-Event-Id";

    private const int DefaultTimestampToleranceSeconds = 300;
    private const int MinimumSecretLength = 32;

    public static async Task<HrWebhookAuthenticationResult> AuthenticateAsync(
        HttpRequest request,
        string rawBody,
        IConfiguration configuration,
        IHrWebhookReplayStore replayStore,
        CancellationToken ct = default)
    {
        var secret = GetSecret(configuration);
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < MinimumSecretLength)
            return HrWebhookAuthenticationResult.Unauthorized("HR webhook secret is not configured.");

        if (!TryGetSingleHeader(request.Headers, TimestampHeader, out var timestampValue) ||
            !long.TryParse(timestampValue, out var unixSeconds))
        {
            return HrWebhookAuthenticationResult.BadRequest("Missing or invalid HR webhook timestamp.");
        }

        var toleranceSeconds = configuration.GetValue(
            "HrWebhook:TimestampToleranceSeconds",
            DefaultTimestampToleranceSeconds);
        var tolerance = TimeSpan.FromSeconds(Math.Max(1, toleranceSeconds));
        DateTimeOffset signedAt;
        try
        {
            signedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return HrWebhookAuthenticationResult.BadRequest("Missing or invalid HR webhook timestamp.");
        }

        if (DateTimeOffset.UtcNow - signedAt > tolerance || signedAt - DateTimeOffset.UtcNow > tolerance)
            return HrWebhookAuthenticationResult.BadRequest("HR webhook timestamp is outside the allowed tolerance.");

        if (!TryGetSingleHeader(request.Headers, EventIdHeader, out var eventId) || string.IsNullOrWhiteSpace(eventId))
            return HrWebhookAuthenticationResult.BadRequest("Missing HR webhook event id.");

        if (!TryGetSingleHeader(request.Headers, SignatureHeader, out var providedSignature) ||
            string.IsNullOrWhiteSpace(providedSignature))
        {
            return HrWebhookAuthenticationResult.Unauthorized("Missing HR webhook signature.");
        }

        if (!SignatureMatches(secret, timestampValue, rawBody, providedSignature))
            return HrWebhookAuthenticationResult.Unauthorized("Invalid HR webhook signature.");

        var replayTtl = tolerance + TimeSpan.FromMinutes(1);
        if (!await replayStore.TryMarkSeenAsync(eventId, replayTtl, ct))
            return HrWebhookAuthenticationResult.BadRequest("Duplicate HR webhook event id.");

        return HrWebhookAuthenticationResult.Success(eventId);
    }

    public static string ComputeSignature(string secret, string timestamp, string rawBody)
    {
        var payload = Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();
    }

    private static bool SignatureMatches(string secret, string timestamp, string rawBody, string providedSignature)
    {
        var normalizedSignature = providedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? providedSignature["sha256=".Length..]
            : providedSignature;

        var expectedSignature = ComputeSignature(secret, timestamp, rawBody);
        var expectedBytes = Encoding.ASCII.GetBytes(expectedSignature);
        var providedBytes = Encoding.ASCII.GetBytes(normalizedSignature.Trim().ToLowerInvariant());

        return expectedBytes.Length == providedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static string? GetSecret(IConfiguration configuration) =>
        configuration["HrWebhook:Secret"]
        ?? configuration["HrWebhooks:Secret"]
        ?? configuration["IdentityService:HrWebhook:Secret"];

    private static bool TryGetSingleHeader(IHeaderDictionary headers, string name, out string value)
    {
        value = "";
        if (!headers.TryGetValue(name, out StringValues values) || values.Count != 1)
            return false;

        value = values[0] ?? "";
        return !string.IsNullOrWhiteSpace(value);
    }
}

public record HrWebhookAuthenticationResult(bool Succeeded, int StatusCode, string Detail, string? EventId)
{
    public static HrWebhookAuthenticationResult Success(string eventId) => new(true, 200, "", eventId);
    public static HrWebhookAuthenticationResult Unauthorized(string detail) => new(false, 401, detail, null);
    public static HrWebhookAuthenticationResult BadRequest(string detail) => new(false, 400, detail, null);
}

public interface IHrWebhookReplayStore
{
    Task<bool> TryMarkSeenAsync(string eventId, TimeSpan ttl, CancellationToken ct);
}

public sealed class DistributedCacheHrWebhookReplayStore : IHrWebhookReplayStore
{
    private readonly IDistributedCache _cache;

    public DistributedCacheHrWebhookReplayStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<bool> TryMarkSeenAsync(string eventId, TimeSpan ttl, CancellationToken ct)
    {
        var key = $"identity:hr-webhook:event:{eventId}";
        if (await _cache.GetStringAsync(key, ct) is not null)
            return false;

        await _cache.SetStringAsync(
            key,
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            ct);
        return true;
    }
}

public sealed class RedisHrWebhookReplayStore : IHrWebhookReplayStore
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHrWebhookReplayStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> TryMarkSeenAsync(string eventId, TimeSpan ttl, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        return await db.StringSetAsync(
            $"identity:hr-webhook:event:{eventId}",
            "1",
            ttl,
            When.NotExists);
    }
}

public sealed class BoundedInMemoryHrWebhookReplayStore : IHrWebhookReplayStore
{
    public static readonly BoundedInMemoryHrWebhookReplayStore Shared = new(10_000);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _seenEvents = new();
    private readonly int _maxEntries;

    public BoundedInMemoryHrWebhookReplayStore(int maxEntries)
    {
        _maxEntries = maxEntries;
    }

    public Task<bool> TryMarkSeenAsync(string eventId, TimeSpan ttl, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        PruneExpired(now);

        if (_seenEvents.Count >= _maxEntries)
            PruneOldest();

        return Task.FromResult(_seenEvents.TryAdd(eventId, now.Add(ttl)));
    }

    private void PruneExpired(DateTimeOffset now)
    {
        foreach (var entry in _seenEvents)
        {
            if (entry.Value <= now)
                _seenEvents.TryRemove(entry.Key, out _);
        }
    }

    private void PruneOldest()
    {
        foreach (var key in _seenEvents
            .OrderBy(entry => entry.Value)
            .Take(Math.Max(1, _maxEntries / 10))
            .Select(entry => entry.Key))
        {
            _seenEvents.TryRemove(key, out _);
        }
    }
}
