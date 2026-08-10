using System.Security.Claims;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class IdentityBrokerService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<IdentityBrokerService> _logger;

    public IdentityBrokerService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<IdentityBrokerService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    public async Task<(User User, bool IsNew, string? Error)> FindOrCreateExternalUserAsync(
        ClaimsPrincipal externalPrincipal, string provider, CancellationToken ct = default)
    {
        var email = externalPrincipal.FindFirstValue(ClaimTypes.Email);
        var name = externalPrincipal.FindFirstValue(ClaimTypes.Name);
        var providerKey = externalPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(email))
            return (null!, false, "External provider did not return an email address");

        if (!string.IsNullOrEmpty(providerKey))
        {
            var userByLogin = await _userManager.FindByLoginAsync(provider, providerKey);
            if (userByLogin is not null)
            {
                _logger.LogInformation("User {UserId} signed in via {Provider} (existing link)",
                    userByLogin.Id, provider);
                return (userByLogin, false, null);
            }
        }

        var userByEmail = await _userManager.FindByEmailAsync(email);

        if (userByEmail is not null)
        {
            if (!string.IsNullOrEmpty(providerKey))
            {
                await _userManager.AddLoginAsync(userByEmail,
                    new UserLoginInfo(provider, providerKey!, provider));
                _logger.LogInformation("Linked {Provider} to existing user {UserId}",
                    provider, userByEmail.Id);
            }
            return (userByEmail, false, null);
        }

        var newUser = ProvisionUserFromClaims(externalPrincipal, email, name);

        var result = await _userManager.CreateAsync(newUser);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create user from {Provider}: {Errors}", provider, errors);
            return (null!, false, errors);
        }

        await _userManager.AddToRoleAsync(newUser, "Provider");

        if (!string.IsNullOrEmpty(providerKey))
        {
            await _userManager.AddLoginAsync(newUser,
                new UserLoginInfo(provider, providerKey!, provider));
        }

        _logger.LogInformation("Provisioned new user {UserId} from {Provider} ({Email})",
            newUser.Id, provider, email);

        return (newUser, true, null);
    }

    public List<Claim> TransformClaims(ClaimsPrincipal externalPrincipal, string provider)
    {
        var claims = new List<Claim>();

        foreach (var claimType in new[] { ClaimTypes.Email, ClaimTypes.Name, ClaimTypes.GivenName, ClaimTypes.Surname })
        {
            var value = externalPrincipal.FindFirstValue(claimType);
            if (!string.IsNullOrEmpty(value))
                claims.Add(new Claim(claimType, value));
        }

        claims.Add(new Claim("identity_provider", provider));
        claims.Add(new Claim("auth_method", "federated"));

        if (provider == "Google")
        {
            var picture = externalPrincipal.FindFirstValue("picture");
            if (!string.IsNullOrEmpty(picture))
                claims.Add(new Claim("picture", picture));
        }

        if (provider == "Microsoft")
        {
            var tenantId = externalPrincipal.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid");
            if (!string.IsNullOrEmpty(tenantId))
                claims.Add(new Claim("ms_tenant_id", tenantId));
        }

        return claims;
    }

    private static User ProvisionUserFromClaims(ClaimsPrincipal principal, string email, string? name)
    {
        var firstName = principal.FindFirstValue(ClaimTypes.GivenName)
                     ?? name?.Split(' ').FirstOrDefault()
                     ?? email.Split('@')[0];

        var lastName = principal.FindFirstValue(ClaimTypes.Surname)
                    ?? name?.Split(' ').Skip(1).LastOrDefault()
                    ?? "";

        return new User
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
