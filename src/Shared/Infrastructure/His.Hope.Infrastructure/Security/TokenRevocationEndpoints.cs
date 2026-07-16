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
    public static RouteGroupBuilder MapTokenRevocationEndpoints(
        this RouteGroupBuilder group)
    {
        group.MapPost("/revoke", async (
            RevokeTokenRequest request,
            ITokenBlacklistService blacklistService,
            ILogger<ITokenBlacklistService> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Jti))
                return Results.BadRequest(new { error = "jti is required" });

            var ttl = request.TtlSeconds > 0
                ? TimeSpan.FromSeconds(request.TtlSeconds)
                : TimeSpan.FromHours(1);

            await blacklistService.RevokeAsync(request.Jti, ttl, ct);
            logger.LogInformation("Token revoked via API: jti={Jti}", request.Jti);
            return Results.Ok(new { revoked = true, jti = request.Jti });
        })
        .RequireAuthorization()
        .WithOpenApi();

        group.MapPost("/revoke-all", async (
            RevokeAllUserTokensRequest request,
            ITokenBlacklistService blacklistService,
            ILogger<ITokenBlacklistService> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
                return Results.BadRequest(new { error = "userId is required" });

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
