using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Api.Composition;

namespace His.Hope.IdentityService.Api.Endpoints;

internal static class IdentityLegacyAuthEndpoints
{
    public static void MapIdentityLegacyAuthEndpoints(this RouteGroupBuilder auth)
    {
        auth.MapPost("/ldap/login", async (LdapLoginRequest request, LdapSyncService ldap,
            OidcLoginCompletionService completion, HttpContext context, CancellationToken ct) =>
        {
            var profile = await ldap.AuthenticateAndGetProfileAsync(request.UserName, request.Password, ct);
            if (profile is null || !profile.IsActive)
                return Results.Unauthorized();

            var user = await ldap.ProvisionUserAsync(profile, ct);
            if (!user.IsActive)
                return Results.Unauthorized();

            var completed = await completion.CompletePrimaryAsync(context, user, "/", ["ldap"], ct);
            return Results.Ok(new { authenticated = !completed.RequiresMfa, requiresMfa = completed.RequiresMfa, mfaUrl = completed.RedirectUrl, userId = user.Id });
        })
        .WithDeprecationNotice()
        .WithOpenApi()
        .RequireRateLimiting("auth")
        .AllowAnonymous();

        auth.MapPost("/register", async (RegisterRequest request, IIdentityService identityService, CancellationToken ct) =>
        {
            try
            {
                var result = await identityService.RegisterAsync(request, ct);
                return Results.Created("/api/v1/auth/me", result);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["errorCode"] = ApiErrorCodes.AuthenticationRequestInvalid });
            }
        })
        .WithOpenApi()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .AllowAnonymous();
    }
}
