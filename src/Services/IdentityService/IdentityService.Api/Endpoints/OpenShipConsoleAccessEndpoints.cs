using His.Hope.Contracts;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.IdentityService.Application.OpenShip;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Console-access lookup for the OpenShip tailnet console gateway. IdentityService
/// is the source of truth: IAM groups prefixed with the configured console prefix
/// are the console roles and an approved break-glass request for the console
/// write permission is the only source of (time-boxed) write access.
/// </summary>
public static class OpenShipConsoleAccessEndpoints
{
    private const int MaxEmailLength = 256;

    public static void MapOpenShipConsoleAccessEndpoints(this WebApplication app)
    {
        var environment = app.Services.GetRequiredService<IHostEnvironment>();
        app.MapGet(IdentityApiRoutes.OpenShipConsoleAccess, GetConsoleAccess)
            .RequireAuthorization(IdentityServiceRegistrationExtensions.OpenShipConsoleAccessPolicy)
            .AddEndpointFilter(async (context, next) =>
            {
                if (environment.IsProduction() && !context.HttpContext.Request.IsHttps)
                    return Problem(StatusCodes.Status400BadRequest, "https_required", "Console-access lookup requires HTTPS.");
                return await next(context);
            })
            .WithOpenApi();
    }

    internal static async Task<IResult> GetConsoleAccess(
        string? email,
        HttpContext http,
        IdentityDbContext db,
        ILookupNormalizer normalizer,
        IOpenShipConsoleAccessResolver resolver,
        CancellationToken ct)
    {
        // Access decisions must never be cached by an intermediary or the gateway's HTTP stack.
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";

        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || normalizedEmail.Length > MaxEmailLength || !normalizedEmail.Contains('@'))
            return Problem(StatusCodes.Status400BadRequest, ApiErrorCodes.Validation, "A valid email query parameter is required.");

        var lookupKey = normalizer.NormalizeEmail(normalizedEmail);
        var candidates = await db.Users.AsNoTracking()
            .TagWith("Identity.OpenShip.ConsoleAccessLookup")
            .Where(user => user.NormalizedEmail == lookupKey)
            .Select(user => new { user.Id, user.IsActive })
            .Take(2)
            .ToListAsync(ct);

        // Unknown, ambiguous (duplicate email) and inactive users are all
        // reported as 404 so the gateway denies access and the response does not
        // reveal which case applied.
        if (candidates.Count != 1 || !candidates[0].IsActive)
            return Problem(StatusCodes.Status404NotFound, ApiErrorCodes.NotFound, "No active console user matches this email.");

        var access = await resolver.ResolveAsync(candidates[0].Id, ct);
        return Results.Ok(new OpenShipConsoleAccessResponse(
            normalizedEmail,
            Active: true,
            access.Groups,
            access.WriteUntil is { } writeUntil
                ? DateTime.SpecifyKind(writeUntil, DateTimeKind.Utc).ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                : null));
    }

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["errorCode"] = code });

    /// <summary>Only the console decision is returned; no other user attributes.</summary>
    public sealed record OpenShipConsoleAccessResponse(
        string Email,
        bool Active,
        IReadOnlyList<string> Groups,
        string? WriteUntil);
}
