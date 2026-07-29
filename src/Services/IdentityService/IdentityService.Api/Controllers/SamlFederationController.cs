using System.Security.Authentication;
using System.Security.Claims;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace His.Hope.IdentityService.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/federation/saml")]
public sealed class SamlFederationController(
    SamlRuntimeConfigurationService samlRuntime,
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    OidcLoginCompletionService completion) : ControllerBase
{
    [HttpGet("login")]
    public async Task<IActionResult> Login(string? returnUrl = null, CancellationToken ct = default)
    {
        try
        {
            var (configuration, settings) = await samlRuntime.CreateAsync(ct);
            var binding = new Saml2RedirectBinding();
            var request = new Saml2AuthnRequest(configuration)
            {
                NameIdPolicy = new NameIdPolicy { AllowCreate = true },
                AssertionConsumerServiceUrl = BuildAcsUrl(settings.Issuer)
            };
            if (!string.IsNullOrWhiteSpace(returnUrl))
                binding.SetRelayStateQuery(new Dictionary<string, string> { ["returnUrl"] = returnUrl });
            return binding.Bind(request).ToActionResult();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    [HttpPost("acs")]
    public async Task<IActionResult> AssertionConsumerService(CancellationToken ct = default)
    {
        var (configuration, settings) = await samlRuntime.CreateAsync(ct);
        var httpRequest = Request.ToGenericHttpRequest(validate: true);
        var response = new Saml2AuthnResponse(configuration);
        httpRequest.Binding.ReadSamlResponse(httpRequest, response);
        if (response.Status != Saml2StatusCodes.Success)
            throw new AuthenticationException($"SAML response status: {response.Status}");

        httpRequest.Binding.Unbind(httpRequest, response);
        if (response.ClaimsIdentity is null)
            throw new AuthenticationException("SAML response did not contain an identity");

        // Keycloak can return the email as a regular SAML attribute or as the
        // NameID when the client Name ID format is set to email.
        var email = FindClaim(
            response.ClaimsIdentity,
            settings.EmailClaim,
            ClaimTypes.Email,
            "email",
            ClaimTypes.NameIdentifier,
            "nameidentifier",
            "NameID");
        if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            return Unauthorized("SAML assertion must contain a valid email claim");

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsActive = true,
                FirstName = FindClaim(response.ClaimsIdentity, ClaimTypes.GivenName, "givenName") ?? "Federated",
                LastName = FindClaim(response.ClaimsIdentity, ClaimTypes.Surname, "surname") ?? "User",
                CreatedAt = DateTime.UtcNow
            };
            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
                return Problem("Unable to provision the federated user", statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!user.IsActive)
            return Unauthorized();

        await ApplyMappedRolesAsync(user, response.ClaimsIdentity, settings, ct);
        var relay = httpRequest.Binding.GetRelayStateQuery();
        var returnUrl = relay.TryGetValue("returnUrl", out var value) && IsSafeLocalReturnUrl(value) ? value : "/";
        var result = await completion.CompletePrimaryAsync(HttpContext, user, returnUrl, ["saml"], ct);
        return Redirect(result.RedirectUrl);
    }

    private async Task ApplyMappedRolesAsync(User user, ClaimsIdentity identity, SamlRuntimeSettings settings, CancellationToken ct)
    {
        var groups = identity.FindAll(settings.GroupClaim)
            .SelectMany(claim => claim.Value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
        var mappedRoles = settings.GroupRoleMapping
            .Where(mapping => groups.Any(group => group.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase)))
            .Select(mapping => mapping.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var roles = new List<string>();
        foreach (var role in mappedRoles)
            if (await roleManager.RoleExistsAsync(role))
                roles.Add(role);
        var existing = await userManager.GetRolesAsync(user);
        foreach (var role in roles.Except(existing, StringComparer.OrdinalIgnoreCase))
            await userManager.AddToRoleAsync(user, role);
        foreach (var role in existing.Except(roles, StringComparer.OrdinalIgnoreCase))
            if (role != "Provider")
                await userManager.RemoveFromRoleAsync(user, role);
    }

    private static string? FindClaim(ClaimsIdentity identity, params string[] types) =>
        types.Select(identity.FindFirst).FirstOrDefault(claim => !string.IsNullOrWhiteSpace(claim?.Value))?.Value;

    private static Uri BuildAcsUrl(string issuer)
    {
        if (Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
            return new Uri($"{issuerUri.GetLeftPart(UriPartial.Authority)}/api/v1/federation/saml/acs");

        throw new InvalidOperationException("SAML issuer must be an absolute URL to build the ACS endpoint.");
    }

    private static bool IsSafeLocalReturnUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith("/", StringComparison.Ordinal) &&
        !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\') && !value.Contains(':');
}
