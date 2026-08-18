using System.Text.Json.Serialization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using StackExchange.Redis;
using His.Hope.IdentityService.Api.Services;
using His.Hope.Contracts.Identity;
using His.Hope.Contracts;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class MobilePlatformEndpoints
{
    public static void MapMobilePlatformEndpoints(this WebApplication app)
    {
        var mobile = app.MapGroup("/api/v1/mobile");

        mobile.MapGet("/app-policy", (IConfiguration configuration) =>
        {
            var section = configuration.GetSection("Mobile:AppPolicy");
            return Results.Ok(new AppPolicyResponse(
                section["MinimumVersion"] ?? "1.0.0",
                section["LatestVersion"] ?? section["MinimumVersion"] ?? "1.0.0",
                section.GetValue("ForceUpgrade", false),
                section["StoreUrl"],
                section.GetValue("Maintenance", false)));
        }).AllowAnonymous();

        mobile.MapPost("/push-tokens", async (
            PushTokenRequest request,
            HttpContext context,
            IdentityDbContext db,
            IDataProtectionProvider protectionProvider,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 4096)
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.InvalidPushToken });
            if (request.Platform is not ("android" or "ios"))
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UnsupportedMobilePlatform });

            var userId = GetUserId(context);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();

            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
            var registration = await db.MobileDeviceRegistrations.SingleOrDefaultAsync(
                device => device.UserId == userId && device.Platform == request.Platform && device.TokenHash == tokenHash,
                cancellationToken);
            if (registration is null)
            {
                registration = new MobileDeviceRegistration
                {
                    UserId = userId,
                    Platform = request.Platform,
                    TokenHash = tokenHash
                };
                db.MobileDeviceRegistrations.Add(registration);
            }
            // Re-protect on every registration so a revoked device or a
            // registration after DataProtection key rotation becomes usable
            // again without retaining stale ciphertext.
            registration.TokenCiphertext = protectionProvider
                .CreateProtector("HisHope.Mobile.PushToken.v1")
                .Protect(request.Token);
            registration.LastSeenAt = DateTime.UtcNow;
            registration.RevokedAt = null;
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization();

        mobile.MapGet("/notifications", async (
            int? page,
            int? pageSize,
            HttpContext context,
            IdentityDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (GetUserId(context) is not { Length: > 0 } userId)
                return Results.Unauthorized();

            var currentPage = Math.Max(1, page ?? 1);
            var size = Math.Clamp(pageSize ?? 20, 1, 100);
            var query = db.InAppNotifications
                .AsNoTracking()
                .Where(item => item.UserId == userId);
            var total = await query.CountAsync(cancellationToken);
            var unread = await query.CountAsync(item => item.ReadAt == null, cancellationToken);
            var items = await query
                .OrderByDescending(item => item.CreatedAt)
                .Skip((currentPage - 1) * size)
                .Take(size)
                .Select(item => new
                {
                    id = item.Id,
                    title = item.Title,
                    body = item.Body,
                    dataJson = item.DataJson,
                    createdAt = item.CreatedAt,
                    readAt = item.ReadAt
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new { items, page = currentPage, pageSize = size, total, unread });
        }).RequireAuthorization();

        mobile.MapPost("/notifications/{id:guid}/read", async (
            Guid id,
            HttpContext context,
            IdentityDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (GetUserId(context) is not { Length: > 0 } userId)
                return Results.Unauthorized();

            var updated = await db.InAppNotifications
                .Where(item => item.Id == id && item.UserId == userId && item.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAt, DateTime.UtcNow), cancellationToken);
            return updated == 0 ? Results.NotFound() : Results.NoContent();
        }).RequireAuthorization();

        mobile.MapPost("/notifications/read-all", async (
            HttpContext context,
            IdentityDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (GetUserId(context) is not { Length: > 0 } userId)
                return Results.Unauthorized();

            var updated = await db.InAppNotifications
                .Where(item => item.UserId == userId && item.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAt, DateTime.UtcNow), cancellationToken);
            return Results.Ok(new { updated });
        }).RequireAuthorization();

        mobile.MapPost("/crash-reports", async (MobileCrashReport report, HttpContext context, IdentityDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(report.Message) || report.Message.Length > 2000)
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.InvalidCrashReport });
            Activity.Current?.SetTag("mobile.telemetry.type", "crash");
            Activity.Current?.SetTag("mobile.app.version", report.AppVersion[..Math.Min(report.AppVersion.Length, 50)]);
            Activity.Current?.SetTag("mobile.platform", report.Platform[..Math.Min(report.Platform.Length, 20)]);
            Activity.Current?.SetStatus(ActivityStatusCode.Error, "mobile crash report");
            loggerFactory.CreateLogger("MobileCrashReport").LogError(
                "Mobile crash: platform={Platform}, version={Version}, route={Route}, message={Message}",
                report.Platform, report.AppVersion, report.Route, report.Message);
            db.MobileTelemetryEvents.Add(new MobileTelemetryEvent
            {
                EventType = "crash",
                Name = "unhandled",
                Message = report.Message,
                Stack = report.Stack is null ? null : report.Stack[..Math.Min(report.Stack.Length, 8000)],
                Route = report.Route is null ? null : report.Route[..Math.Min(report.Route.Length, 500)],
                AppVersion = report.AppVersion[..Math.Min(report.AppVersion.Length, 50)],
                Platform = report.Platform[..Math.Min(report.Platform.Length, 20)],
                CorrelationId = context.TraceIdentifier
            });
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }).AllowAnonymous();

        mobile.MapPost("/rum", async (MobileRumEvent rum, HttpContext context, IdentityDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(rum.Name) || rum.Name.Length > 120)
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.InvalidRumEvent });
            Activity.Current?.SetTag("mobile.telemetry.type", "rum");
            Activity.Current?.SetTag("mobile.rum.name", rum.Name);
            Activity.Current?.SetTag("mobile.rum.duration_ms", rum.DurationMs);
            Activity.Current?.SetTag("mobile.app.version", rum.AppVersion[..Math.Min(rum.AppVersion.Length, 50)]);
            Activity.Current?.SetTag("mobile.platform", rum.Platform[..Math.Min(rum.Platform.Length, 20)]);
            loggerFactory.CreateLogger("MobileRum").LogInformation(
                "Mobile RUM: name={Name}, durationMs={DurationMs}, platform={Platform}, version={Version}, route={Route}",
                rum.Name, rum.DurationMs, rum.Platform, rum.AppVersion, rum.Route);
            db.MobileTelemetryEvents.Add(new MobileTelemetryEvent
            {
                EventType = "rum",
                Name = rum.Name,
                Route = rum.Route is null ? null : rum.Route[..Math.Min(rum.Route.Length, 500)],
                AppVersion = rum.AppVersion[..Math.Min(rum.AppVersion.Length, 50)],
                Platform = rum.Platform[..Math.Min(rum.Platform.Length, 20)],
                DurationMs = rum.DurationMs,
                MetadataJson = rum.Metadata is null ? null : JsonSerializer.Serialize(rum.Metadata),
                CorrelationId = context.TraceIdentifier
            });
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }).AllowAnonymous();

        mobile.MapPost("/sync", async (
            SyncEnvelope envelope,
            HttpContext context,
            IConnectionMultiplexer redis,
            IConfiguration configuration) =>
        {
            if (GetUserId(context) is not { Length: > 0 } userId)
                return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(envelope.IdempotencyKey) || envelope.IdempotencyKey.Length > 128 ||
                string.IsNullOrWhiteSpace(envelope.Operation) || envelope.Operation.Length > 120 ||
                envelope.Payload.Count > 100 || JsonSerializer.Serialize(envelope.Payload).Length > 100_000)
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.InvalidSyncEnvelope });
            if (envelope.SchemaVersion is not (null or 1) ||
                envelope.ConflictPolicy is not (null or "reject_on_stale" or "last_write_wins"))
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UnsupportedSyncContract });
            if (envelope.EntityType?.StartsWith("patient", StringComparison.OrdinalIgnoreCase) == true &&
                !string.Equals(envelope.ConflictPolicy, "reject_on_stale", StringComparison.Ordinal))
                return Results.Conflict(new { errorCode = "patient_sync_requires_reject_on_stale" });
            if (envelope.EntityType?.StartsWith("patient", StringComparison.OrdinalIgnoreCase) == true &&
                !configuration.GetValue("Mobile:Offline:PatientDataEnabled", false))
                return Results.Conflict(new { errorCode = "offline_patient_data_disabled" });

            var key = $"mobile:sync:{userId}:{envelope.IdempotencyKey}";
            var accepted = await redis.GetDatabase().StringSetAsync(key, "accepted", TimeSpan.FromDays(7), When.NotExists);
            return accepted ? Results.Accepted() : Results.Ok(new { duplicate = true });
        }).RequireAuthorization();

        var adminPush = app.MapGroup("/api/v1/admin/push")
            .RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite);
        var adminPushRead = app.MapGroup("/api/v1/admin/push")
            .RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead);
        var adminMobile = app.MapGroup("/api/v1/admin/mobile")
            .RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead);

        adminMobile.MapGet("/devices", async (
            int? page,
            int? pageSize,
            IdentityDbContext db,
            CancellationToken cancellationToken) =>
        {
            var currentPage = Math.Max(1, page ?? 1);
            var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var query = db.MobileDeviceRegistrations.AsNoTracking();
            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(device => device.LastSeenAt)
                .Skip((currentPage - 1) * size)
                .Take(size)
                .Select(device => new
                {
                    id = device.Id,
                    userId = device.UserId,
                    platform = device.Platform,
                    registeredAt = device.RegisteredAt,
                    lastSeenAt = device.LastSeenAt,
                    revokedAt = device.RevokedAt,
                    active = device.RevokedAt == null
                })
                .ToListAsync(cancellationToken);
            return Results.Ok(new { items, page = currentPage, pageSize = size, total });
        });

        adminMobile.MapPost("/devices/{id:guid}/revoke", async (
            Guid id,
            IdentityDbContext db,
            CancellationToken cancellationToken) =>
        {
            var updated = await db.MobileDeviceRegistrations
                .Where(device => device.Id == id && device.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(device => device.RevokedAt, DateTime.UtcNow), cancellationToken);
            return updated == 0 ? Results.NotFound() : Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite);

        adminPushRead.MapGet("/delivery-summary", async (
            int? hours,
            IdentityDbContext db,
            CancellationToken cancellationToken) =>
        {
            var since = DateTime.UtcNow.AddHours(-Math.Clamp(hours ?? 24, 1, 168));
            var attempts = db.PushDeliveryAttempts.AsNoTracking().Where(item => item.CreatedAt >= since);
            var outbox = db.PushNotificationOutbox.AsNoTracking().Where(item => item.CreatedAt >= since);
            return Results.Ok(new
            {
                since,
                queued = await outbox.CountAsync(cancellationToken),
                processed = await outbox.CountAsync(item => item.ProcessedAt != null, cancellationToken),
                pending = await outbox.CountAsync(item => item.ProcessedAt == null, cancellationToken),
                sent = await attempts.CountAsync(item => item.Status == "sent", cancellationToken),
                failed = await attempts.CountAsync(item => item.Status == "failed", cancellationToken),
                byPlatform = await attempts.GroupBy(item => item.Platform)
                    .Select(group => new { platform = group.Key, sent = group.Count(item => item.Status == "sent"), failed = group.Count(item => item.Status == "failed") })
                    .ToListAsync(cancellationToken),
                lastFailure = await attempts.Where(item => item.Status == "failed")
                    .OrderByDescending(item => item.CreatedAt)
                    .Select(item => new { item.Platform, item.ErrorCode, item.CreatedAt })
                    .FirstOrDefaultAsync(cancellationToken)
            });
        });
        adminPush.MapPost("/notifications", async (
            PushNotificationRequest request,
            IPushDeliveryService delivery,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || request.UserId.Length > 200 ||
                string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200 ||
                string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 4000)
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.InvalidPushNotification });

            var id = await delivery.EnqueueAsync(request.UserId, request.Title, request.Body, request.DataJson, cancellationToken);
            return Results.Accepted($"{IdentityApiRoutes.AdminPushNotifications}/{id}", new { id });
        });
    }

    public sealed record AppPolicyResponse(
        string MinimumVersion,
        string LatestVersion,
        bool ForceUpgrade,
        string? StoreUrl,
        bool Maintenance);

    public sealed record PushTokenRequest(string Token, string Platform);

    public sealed record MobileCrashReport(
        string Message,
        string? Stack,
        string? Route,
        string AppVersion,
        string Platform,
        string? CorrelationId);

    public sealed record MobileRumEvent(
        string Name,
        double? DurationMs,
        string? Route,
        string AppVersion,
        string Platform,
        Dictionary<string, string>? Metadata);

    public sealed record SyncEnvelope(
        string IdempotencyKey,
        string Operation,
        Dictionary<string, object?> Payload,
        DateTime CreatedAt,
        int? SchemaVersion = 1,
        string? EntityType = null,
        string? EntityId = null,
        string? BaseVersion = null,
        string? ConflictPolicy = "reject_on_stale");

    public sealed record PushNotificationRequest(string UserId, string Title, string Body, string? DataJson = null);

    private static string? GetUserId(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.User.FindFirstValue("sub");
}
