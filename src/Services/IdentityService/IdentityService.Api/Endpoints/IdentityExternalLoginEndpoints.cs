using System.Security.Claims;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Application.Security;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.SharedKernel.Protocol;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace His.Hope.IdentityService.Api.Endpoints;

internal static class IdentityExternalLoginEndpoints
{
    public static void MapIdentityExternalLoginEndpoints(this RouteGroupBuilder auth)
    {
        auth.MapGet("/external-login/{provider}", (string provider, HttpContext httpContext, IConfiguration configuration) =>
        {
            // Never challenge an arbitrary authentication scheme chosen by the caller.
            if (!IsSupportedProvider(provider, configuration))
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UnsupportedExternalProvider });

            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/api/v1/auth/external-callback/{provider}"
            };
            properties.Items["LoginProvider"] = provider;
            var returnUrl = httpContext.Request.Query["returnUrl"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith("/", StringComparison.Ordinal) && !returnUrl.StartsWith("//", StringComparison.Ordinal))
                properties.Items["returnUrl"] = returnUrl;
            return Results.Challenge(properties, new[] { provider });
        }).AllowAnonymous();

        auth.MapGet("/external-callback/{provider}", async (
            string provider,
            HttpContext httpContext,
            IConfiguration configuration,
            UserManager<User> userManager,
            OidcLoginCompletionService completion,
            IHostEnvironment environment,
            CancellationToken ct) =>
        {
            if (!IsSupportedProvider(provider, configuration))
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UnsupportedExternalProvider });

            var result = await httpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!result.Succeeded)
                return Results.Redirect("/login?error=external_failed");

            // The external ticket is single-use: drop it before doing anything else
            // so the callback cannot be replayed to re-establish a session.
            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            // SECURITY: the provider in the route selects which linked account is
            // looked up. It must be the provider that actually issued the ticket,
            // otherwise one IdP could assert subjects on behalf of another, and a
            // ticket created for account linking must not sign anyone in.
            var ticketItems = result.Properties?.Items;
            if (ticketItems is null ||
                !ticketItems.TryGetValue("LoginProvider", out var ticketProvider) ||
                !string.Equals(ticketProvider, provider, StringComparison.Ordinal) ||
                ticketItems.ContainsKey("IsLinking"))
                return Results.Redirect("/login?error=external_failed");

            var externalPrincipal = result.Principal;
            var email = externalPrincipal.FindFirstValue(ClaimTypes.Email);
            var name = externalPrincipal.FindFirstValue(ClaimTypes.Name);
            var providerKey = externalPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(email) || string.IsNullOrWhiteSpace(providerKey))
                return Results.Redirect("/login?error=no_email");

            // An upstream subject, not an email claim, is the account-linking key.
            var user = await userManager.FindByLoginAsync(provider, providerKey);
            var emailUser = user is null ? await userManager.FindByEmailAsync(email) : null;
            if (user is null && emailUser is not null)
                return Results.Redirect("/login?error=external_account_link_required");

            if (user is null)
            {
                // SECURITY: an unknown upstream identity must not become a clinical
                // account on its own. Just-in-time creation is opt-in outside
                // Development/Testing; otherwise an administrator provisions the
                // account and the user links the provider from their profile.
                var autoCreate = configuration.GetValue(
                    "Authentication:ExternalProvisioning:AutoCreateUsers",
                    environment.IsDevelopment() || environment.IsEnvironment("Testing"));
                if (!autoCreate)
                    return Results.Redirect("/login?error=external_account_not_provisioned");

                user = new User
                {
                    UserName = email,
                    Email = email,
                    FirstName = name?.Split(' ').FirstOrDefault() ?? email,
                    LastName = name?.Split(' ').Skip(1).LastOrDefault() ?? "",
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                    return Results.Redirect("/login?error=registration_failed");
                await userManager.AddToRoleAsync(user, "Provider");
            }

            var existingLogins = await userManager.GetLoginsAsync(user);
            if (!existingLogins.Any(login => login.LoginProvider == provider && login.ProviderKey == providerKey))
                await userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));

            var returnUrl = result.Properties?.Items.TryGetValue("returnUrl", out var storedReturnUrl) == true
                ? storedReturnUrl
                : httpContext.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            returnUrl = AuthenticationRedirectValidator.ResolveSafeReturnUrl(
                returnUrl,
                configuration,
                httpContext.Request.Headers.Referer.FirstOrDefault(),
                httpContext.Request.Query["spaOrigin"].FirstOrDefault());
            var completed = await completion.CompletePrimaryAsync(httpContext, user, returnUrl, [provider], ct);
            return Results.Redirect(completed.RedirectUrl);
        }).AllowAnonymous();

        static bool IsSupportedProvider(string provider, IConfiguration configuration) =>
            provider is "Google" or "Microsoft" or "Entra" ||
            configuration.GetSection("Authentication:ExternalSources").GetChildren()
                .Select(section => section["Name"])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Contains(provider, StringComparer.Ordinal);

        auth.MapGet("/external-providers", async (
            IConfiguration config,
            ExternalIdentityProviderRuntime externalIdentityRuntime,
            CancellationToken ct) =>
        {
            var providers = new List<object>();
            if (!string.IsNullOrEmpty(config["Authentication:Google:ClientId"]))
                providers.Add(new { provider = "Google", displayName = "Google", icon = "google" });
            if (!string.IsNullOrEmpty(config["Authentication:Microsoft:ClientId"]))
                providers.Add(new { provider = "Microsoft", displayName = "Microsoft", icon = "microsoft" });
            if (!string.IsNullOrEmpty(config["Authentication:Entra:ClientId"]) && Uri.TryCreate(config["Authentication:Entra:Authority"], UriKind.Absolute, out _))
                providers.Add(new { provider = "Entra", displayName = "Microsoft Entra ID", icon = "microsoft" });
            foreach (var source in config.GetSection("Authentication:ExternalSources").GetChildren())
            {
                var name = source["Name"];
                if (!string.IsNullOrWhiteSpace(name) && Uri.TryCreate(source["Authority"], UriKind.Absolute, out var authority) && authority.Scheme == Uri.UriSchemeHttps)
                    providers.Add(new { provider = name, displayName = source["DisplayName"] ?? name, icon = "openid" });
            }
            var saml = await externalIdentityRuntime.GetSamlAsync(ct);
            if (saml.Enabled && !string.IsNullOrWhiteSpace(saml.IdpMetadata))
                providers.Add(new { provider = "Saml", displayName = "SAML SSO", icon = "business", protocol = "saml", loginUrl = "/api/v1/federation/saml/login" });
            return Results.Ok(new { providers });
        }).AllowAnonymous();
    }
}
