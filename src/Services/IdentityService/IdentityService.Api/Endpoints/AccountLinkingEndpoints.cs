using System.Security.Claims;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class AccountLinkingEndpoints
{
    public static RouteGroupBuilder MapAccountLinkingEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/linked-accounts", GetLinkedAccounts);
        group.MapDelete("/linked-accounts/{provider}", UnlinkAccount);
        group.MapGet("/link/{provider}", ChallengeLink);
        group.MapGet("/link-callback/{provider}", HandleLinkCallback);
        return group;
    }

    private static async Task<Ok<List<LinkedAccountResponse>>> GetLinkedAccounts(
        HttpContext httpContext, UserManager<User> userManager)
    {
        var user = await GetCurrentUser(httpContext, userManager);
        if (user is null) return TypedResults.Ok(new List<LinkedAccountResponse>());

        var logins = await userManager.GetLoginsAsync(user);
        var response = logins.Select(l => new LinkedAccountResponse(
            l.LoginProvider,
            l.ProviderDisplayName ?? l.LoginProvider,
            DateTime.UtcNow
        )).ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> UnlinkAccount(
        string provider, HttpContext httpContext, UserManager<User> userManager)
    {
        var user = await GetCurrentUser(httpContext, userManager);
        if (user is null) return TypedResults.NotFound();

        var logins = await userManager.GetLoginsAsync(user);
        var login = logins.FirstOrDefault(l =>
            l.LoginProvider.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (login is null) return TypedResults.NotFound();

        var hasPassword = await userManager.HasPasswordAsync(user);
        if (logins.Count == 1 && !hasPassword)
            return TypedResults.Problem("Cannot unlink the only login method. Set a password first.", statusCode: 400);

        var result = await userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
        if (!result.Succeeded)
            return TypedResults.Problem("Failed to unlink account", statusCode: 500);

        return TypedResults.NoContent();
    }

    private static Results<ChallengeHttpResult, ProblemHttpResult> ChallengeLink(
        string provider, HttpContext httpContext)
    {
        var supportedProviders = new[] { "Google", "Microsoft" };
        if (!supportedProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
            return TypedResults.Problem($"Unsupported provider: {provider}", statusCode: 400);

        var redirectUrl = $"/api/v1/auth/link-callback/{provider}";
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        properties.Items["LoginProvider"] = provider;
        properties.Items["IsLinking"] = "true";

        return TypedResults.Challenge(properties, new[] { provider });
    }

    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> HandleLinkCallback(
        string provider, HttpContext httpContext,
        UserManager<User> userManager, SignInManager<User> signInManager)
    {
        var result = await httpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (!result.Succeeded)
            return TypedResults.Redirect("/profile?error=link_failed");

        var user = await GetCurrentUser(httpContext, userManager);
        if (user is null)
            return TypedResults.Redirect("/login?error=not_authenticated");

        var providerKey = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(providerKey))
            return TypedResults.Redirect("/profile?error=no_provider_key");

        var existingUser = await userManager.FindByLoginAsync(provider, providerKey);
        if (existingUser is not null && existingUser.Id != user.Id)
            return TypedResults.Redirect("/profile?error=already_linked");

        var loginInfo = new UserLoginInfo(provider, providerKey,
            result.Principal.FindFirstValue(ClaimTypes.GivenName) ?? provider);

        var addResult = await userManager.AddLoginAsync(user, loginInfo);
        if (!addResult.Succeeded)
            return TypedResults.Redirect("/profile?error=link_failed");

        return TypedResults.Redirect("/profile?linked=" + provider);
    }

    private static async Task<User?> GetCurrentUser(HttpContext httpContext, UserManager<User> userManager)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? httpContext.User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId)) return null;
        return await userManager.FindByIdAsync(userId);
    }
}

public record LinkedAccountResponse(string Provider, string DisplayName, DateTime LinkedAt);
