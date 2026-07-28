using System.Text.Json.Serialization;
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
                return Results.BadRequest(new ProblemDetails { Title = "Invalid push token", Status = 400 });
            if (request.Platform is not ("android" or "ios"))
                return Results.BadRequest(new ProblemDetails { Title = "Unsupported platform", Status = 400 });

            var userId = context.User.FindFirst("sub")?.Value;
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
                    TokenHash = tokenHash,
                    TokenCiphertext = protectionProvider.CreateProtector("HisHope.Mobile.PushToken.v1").Protect(request.Token)
                };
                db.MobileDeviceRegistrations.Add(registration);
            }
            registration.LastSeenAt = DateTime.UtcNow;
            registration.RevokedAt = null;
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        // Keep the endpoint anonymous at the middleware level so native/API
        // callers receive JSON 401 instead of the browser cookie redirect.
        // A bearer-authenticated principal is still required in the handler.
        }).AllowAnonymous();

        mobile.MapPost("/crash-reports", async (MobileCrashReport report, HttpContext context, IdentityDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(report.Message) || report.Message.Length > 2000)
                return Results.BadRequest(new ProblemDetails { Title = "Invalid crash report", Status = 400 });
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
                return Results.BadRequest(new ProblemDetails { Title = "Invalid RUM event", Status = 400 });
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
            IConnectionMultiplexer redis) =>
        {
            if (context.User.FindFirst("sub")?.Value is not { Length: > 0 } userId)
                return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(envelope.IdempotencyKey) || envelope.IdempotencyKey.Length > 128 ||
                string.IsNullOrWhiteSpace(envelope.Operation) || envelope.Operation.Length > 120)
                return Results.BadRequest(new ProblemDetails { Title = "Invalid sync envelope", Status = 400 });

            var key = $"mobile:sync:{userId}:{envelope.IdempotencyKey}";
            var accepted = await redis.GetDatabase().StringSetAsync(key, "accepted", TimeSpan.FromDays(7), When.NotExists);
            return accepted ? Results.Accepted() : Results.Ok(new { duplicate = true });
        }).AllowAnonymous();
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
        DateTime CreatedAt);
}
