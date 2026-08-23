using System.Security.Claims;
using His.Hope.Bff.Core.Authentication;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Security;

internal static class BffSessionGuard
{
    internal static async Task<IResult?> ValidateMutatingSessionAsync(
        HttpContext httpContext,
        IConnectionMultiplexer redis,
        SessionTokenProtector tokenProtector,
        bool requireAuthenticatedPrincipal = true)
    {
        if (requireAuthenticatedPrincipal && httpContext.User.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var sessionId = httpContext.Request.Cookies["hishop_sid"];
        if (string.IsNullOrWhiteSpace(sessionId))
            return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["errorCode"] = "session_cookie_required" });

        var sessionJson = await redis.GetDatabase().StringGetAsync($"session:{sessionId}");
        if (!sessionJson.HasValue)
            return Results.Unauthorized();

        SessionData? session;
        try
        {
            session = System.Text.Json.JsonSerializer.Deserialize<SessionData>(sessionJson!);
            if (session is not null)
            {
                session = session with
                {
                    Jwt = tokenProtector.Unprotect(session.Jwt),
                    RefreshToken = tokenProtector.UnprotectOptional(session.RefreshToken)
                };
            }
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return Results.Unauthorized();
        }

        if (session is null || session.IsExpired)
            return Results.Unauthorized();

        var csrfToken = httpContext.Request.Headers["X-CSRF-Token"].FirstOrDefault();
        if (!string.Equals(session.CsrfToken, csrfToken, StringComparison.Ordinal))
            return Results.Forbid();

        var userAgentHash = His.Hope.IdentityService.Api.Composition.BffHelpers.ComputeSha256(
            httpContext.Request.Headers.UserAgent.ToString());
        if (!string.Equals(session.UserAgentHash, userAgentHash, StringComparison.Ordinal))
            return Results.Forbid();

        if (requireAuthenticatedPrincipal)
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(userId) ||
                !string.Equals(userId, session.UserId, StringComparison.OrdinalIgnoreCase))
                return Results.Forbid();
        }

        httpContext.Items["BffSession"] = session;
        httpContext.Items["BffSessionId"] = sessionId;
        return null;
    }
}
