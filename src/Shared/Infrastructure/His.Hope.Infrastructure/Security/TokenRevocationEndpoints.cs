using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace His.Hope.Infrastructure.Security;

public static class TokenRevocationEndpoints
{
    private const int MaxRevocationTtlSeconds = 24 * 60 * 60;

    public static RouteGroupBuilder MapTokenRevocationEndpoints(
        this RouteGroupBuilder group)
    {
        group.MapPost("/revoke", async (
            RevokeTokenRequest request,
            HttpContext httpContext,
            ITokenBlacklistService blacklistService,
            ILogger<ITokenBlacklistService> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Jti))
                return Results.BadRequest(new { error = "jti is required" });

            // SECURITY: a caller may only revoke the token it is presenting.
            // Revoking other principals' tokens is an administrative action
            // served by the audited admin incident endpoints.
            var callerJti = httpContext.User.FindFirst("jti")?.Value;
            if (!string.Equals(callerJti, request.Jti, StringComparison.Ordinal))
                return Results.Forbid();

            var ttl = request.TtlSeconds > 0
                ? TimeSpan.FromSeconds(Math.Min(request.TtlSeconds, MaxRevocationTtlSeconds))
                : TimeSpan.FromHours(1);

            await blacklistService.RevokeAsync(request.Jti, ttl, ct);
            logger.LogInformation("Token revoked via API: jti={Jti}", request.Jti);
            return Results.Ok(new { revoked = true, jti = request.Jti });
        })
        .RequireAuthorization()
        .WithOpenApi();

        group.MapPost("/revoke-all", async (
            RevokeAllUserTokensRequest request,
            HttpContext httpContext,
            ITokenBlacklistService blacklistService,
            ILogger<ITokenBlacklistService> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
                return Results.BadRequest(new { error = "userId is required" });

            // SECURITY: self-service only. Without this check any authenticated
            // user could keep another user's (e.g. an administrator's) tokens revoked.
            var callerId = httpContext.User.FindFirst("sub")?.Value
                ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(callerId) ||
                !string.Equals(callerId, request.UserId, StringComparison.OrdinalIgnoreCase))
                return Results.Forbid();

            await blacklistService.RevokeAllUserTokensAsync(request.UserId, ct);
            logger.LogWarning("All tokens revoked for user: UserId={UserId}", request.UserId);
            return Results.Ok(new { revoked = true, userId = request.UserId });
        })
        .RequireAuthorization()
        .WithOpenApi();

        return group;
    }
}

public sealed record RevokeTokenRequest
{
    public string Jti { get; init; } = string.Empty;
    public int TtlSeconds { get; init; }
}

public sealed record RevokeAllUserTokensRequest
{
    public string UserId { get; init; } = string.Empty;
}
