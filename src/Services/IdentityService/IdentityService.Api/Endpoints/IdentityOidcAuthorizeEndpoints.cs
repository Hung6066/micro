using System.Security.Claims;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.Security;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Protocol;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using OpenIddictEntityFrameworkCore = OpenIddict.EntityFrameworkCore.Models;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class IdentityOidcAuthorizeEndpoints
{
    public static void MapIdentityOidcAuthorizeEndpoints(this WebApplication app)
    {
        app.MapGet(IdentityApiRoutes.OidcAuthorize, async (
            HttpContext context,
            SignInManager<User> signInManager,
            UserManager<User> userManager,
            IOpenIddictScopeManager scopeManager,
            IdentityDbContext db,
            OidcLoginCompletionService completion) =>
        {
            // Access the OpenIddict server transaction to get the validated request
            var feature = context.Features.Get<OpenIddictServerAspNetCoreFeature>();
            var request = feature?.Transaction?.Request
                ?? throw new InvalidOperationException("OpenIddict request not found.");

            // User must be authenticated (cookie from earlier login)
            if (context.User.Identity is not { IsAuthenticated: true })
            {
                return Results.Challenge(new AuthenticationProperties
                {
                    RedirectUri = context.Request.Path + context.Request.QueryString
                }, new[] { IdentityConstants.ApplicationScheme });
            }

            var user = await userManager.GetUserAsync(context.User)
                ?? throw new InvalidOperationException("Authenticated user not found.");

            // A valid application cookie is not proof that the current OIDC
            // authorization transaction completed MFA. Re-enter the shared MFA gate
            // unless the cookie carries a verified second-factor amr claim.
            var mfaEnabled = user.TwoFactorEnabled || await db.UserMfas
                .AsNoTracking()
                .AnyAsync(item => item.UserId == user.Id && item.IsEnabled, context.RequestAborted);
            if (mfaEnabled && !HasCompletedMfa(context.User))
            {
                var authorizeReturnUrl = context.Request.Path + context.Request.QueryString;
                var authenticationMethods = context.User.FindAll(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod)
                    .Select(claim => claim.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (authenticationMethods.Length == 0)
                    authenticationMethods = ["pwd"];

                var mfa = await completion.CompletePrimaryAsync(
                    context,
                    user,
                    authorizeReturnUrl,
                    authenticationMethods,
                    context.RequestAborted);
                return Results.Redirect(mfa.RedirectUrl);
            }

            var requestedScopes = request.GetScopes().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var existingConsent = await db.ClientConsents
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == user.Id && c.ClientId == request.ClientId && c.IsActive, context.RequestAborted);
            var grantedScopes = existingConsent is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : ParseConsentScopes(existingConsent.Scopes);

            if (!requestedScopes.IsSubsetOf(grantedScopes))
            {
                var consentReturnUrl = context.Request.Path + context.Request.QueryString;
                return Results.Redirect($"/Account/Consent?returnUrl={Uri.EscapeDataString(consentReturnUrl)}");
            }

            var principal = await signInManager.CreateUserPrincipalAsync(user);

            // Ensure sub claim is set (OpenIddict requires it on the principal directly)
            principal.SetClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());
            principal.SetClaim(AuthorizationConstants.Claims.PrincipalType, AuthorizationConstants.PrincipalTypes.Human);

            // Carry the interactive authentication time into the OIDC principal
            // so downstream assurance policies can enforce fresh step-up MFA.
            var authenticationTime = context.User.FindFirst("auth_time")?.Value;
            if (long.TryParse(authenticationTime, out var authenticationTimeValue))
            {
                principal.SetClaim("auth_time", authenticationTimeValue);
            }

            // Preserve the authentication methods completed in the interactive
            // cookie. CreateUserPrincipalAsync builds a fresh principal from the
            // user record and otherwise drops amr=otp/passkey, causing Angular/mobile
            // MFA status checks to reject a session that already passed MFA.
                var completedAuthenticationMethods = context.User.FindAll(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod)
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (principal.Identity is ClaimsIdentity principalIdentity)
            {
                foreach (var method in completedAuthenticationMethods)
                {
                    if (!principalIdentity.FindAll(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod).Any(claim =>
                            string.Equals(claim.Value, method, StringComparison.OrdinalIgnoreCase)))
                    {
                        principalIdentity.AddClaim(new Claim(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod, method));
                    }
                }

                // Keep the verified second factor explicit and scalar in the OIDC
                // token. This avoids claim serializers collapsing multiple amr values
                // into a shape that the Angular/mobile status endpoint cannot read.
                var verifiedSecondFactor = completedAuthenticationMethods.FirstOrDefault(
                    method => string.Equals(method, "passkey", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(method, "otp", StringComparison.OrdinalIgnoreCase));
                if (verifiedSecondFactor is not null)
                    principal.SetClaim(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod, verifiedSecondFactor);
            }

            principal.SetScopes(request.GetScopes());

            // Permissions are derived from the persisted roles at authorization time
            // so every newly issued access token carries the same policy data used by
            // the admin APIs. This is intentionally done before SignIn: token-request
            // handlers are not guaranteed to run for every authorization-code flow.
            var roleNames = (await userManager.GetRolesAsync(user))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (principal.Identity is ClaimsIdentity roleIdentity)
            {
                foreach (var roleName in roleNames)
                {
                    if (!roleIdentity.FindAll(OpenIddictConstants.Claims.Role)
                        .Any(claim => string.Equals(claim.Value, roleName, StringComparison.OrdinalIgnoreCase)))
                    {
                        roleIdentity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, roleName));
                    }
                }
            }
            var permissions = await db.RolePermissions
                .Where(rolePermission => roleNames.Contains(rolePermission.Role.Name!))
                .Select(rolePermission => rolePermission.PermissionCode)
                .Distinct()
                .ToListAsync(context.RequestAborted);
            permissions.AddRange(await db.BreakGlassRequests
                .Where(request => request.SubjectUserId == user.Id && request.Status == "approved" && request.RevokedAt == null && request.ExpiresAt > DateTime.UtcNow)
                .Select(request => request.PermissionCode)
                .ToListAsync(context.RequestAborted));
            permissions = permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (permissions.Count > 0)
                principal.SetClaim(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Permissions, string.Join(",", permissions));

            var resources = new List<string>();
            await foreach (var resource in scopeManager.ListResourcesAsync(principal.GetScopes()))
                resources.Add(resource);
            principal.SetResources(resources);

            // Set claim destinations: required for OpenIddict to accept the principal
            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(claim.Type switch
                {
                    // Identity claims go to access + identity token
                    "name" or "given_name" or "family_name" or "email" => new[] {
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken },
                    // Role claim goes to access token
                    "role" or ClaimTypes.Role => new[] {
                OpenIddictConstants.Destinations.AccessToken },
                    _ => new[] { OpenIddictConstants.Destinations.AccessToken }
                });
            }

            return Results.SignIn(principal,
                properties: new AuthenticationProperties { RedirectUri = request.RedirectUri },
                authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        });

        // ─── OIDC Consent Page ───────────────────────────────────────────────
        // The authorization request has already been validated by OpenIddict when
        // this page is reached. The protected request token prevents form tampering
        // with redirect_uri, client_id, or scopes while the user is deciding.
    }

    private static HashSet<string> ParseConsentScopes(string scopesJson) =>
        new(JsonSerializer.Deserialize<List<string>>(scopesJson) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

    private static bool HasCompletedMfa(ClaimsPrincipal principal) =>
        principal.Claims
            .SelectMany(claim => claim.Value.Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(value =>
                string.Equals(value.Trim('"'), "otp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value.Trim('"'), "passkey", StringComparison.OrdinalIgnoreCase));
}
