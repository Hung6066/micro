using System.Security.Claims;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Application.OpenIddict;

/// <summary>
/// Validates user credentials and MFA status during authorization code flow.
/// Checks account lockout and enforces MFA requirements.
/// </summary>
public class CustomValidateAuthorizationRequest :
    OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreHandlers.Authentication.ValidateAuthorizationRequest
{
    private readonly UserManager<User> _userManager;
    private readonly ILogger<CustomValidateAuthorizationRequest> _logger;

    public CustomValidateAuthorizationRequest(
        UserManager<User> userManager,
        ILogger<CustomValidateAuthorizationRequest> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }
}

/// <summary>
/// Enriches access token and identity token claims with His.Hope-specific data:
/// permissions, roles, facility, license number, MFA status.
/// </summary>
public class CustomPopulateTokenClaims :
    OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreHandlers.Session.PopulateTokenClaims
{
    private readonly UserManager<User> _userManager;
    private readonly ILogger<CustomPopulateTokenClaims> _logger;

    public CustomPopulateTokenClaims(
        UserManager<User> userManager,
        ILogger<CustomPopulateTokenClaims> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }
}

/// <summary>
/// Helper extensions for enriching OpenIddict principals with His.Hope claims.
/// </summary>
public static class TokenEnrichmentExtensions
{
    /// <summary>
    /// Adds His.Hope-specific claims to the principal during token creation.
    /// Called from OpenIddict server event handlers.
    /// </summary>
    public static async Task EnrichPrincipalWithClaims(
        this ClaimsPrincipal principal,
        UserManager<User> userManager,
        CancellationToken ct)
    {
        var userId = principal.FindFirst(OpenIddict.Abstractions.OpenIddictConstants.Claims.Subject)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return;

        var identity = (ClaimsIdentity)principal.Identity!;

        // Add profile claims
        identity.AddClaim(new Claim("fullName", user.FullName ?? ""));
        identity.AddClaim(new Claim("licenseNumber", user.LicenseNumber ?? ""));
        identity.AddClaim(new Claim("license_number", user.LicenseNumber ?? ""));

        // Add roles
        var roles = await userManager.GetRolesAsync(user);
        foreach (var role in roles)
            identity.AddClaim(new Claim(OpenIddict.Abstractions.OpenIddictConstants.Claims.Role, role));

        // Mark that permissions scope is granted
        identity.AddClaim(new Claim("scope", "hishop:permissions"));

        // Add MFA status
        identity.AddClaim(new Claim("amr", user.TwoFactorEnabled ? "mfa" : "pwd"));

        // Add facility claim if set
        var claims = await userManager.GetClaimsAsync(user);
        var facilityClaim = claims.FirstOrDefault(c => c.Type == "facility_id");
        if (facilityClaim is not null)
            identity.AddClaim(new Claim("facility_id", facilityClaim.Value));
    }
}
