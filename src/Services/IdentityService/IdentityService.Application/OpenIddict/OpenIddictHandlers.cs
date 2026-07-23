using System.Security.Claims;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace His.Hope.IdentityService.Application.OpenIddict;

public class CustomValidateAuthorizationRequest :
    IOpenIddictServerHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>
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

    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateAuthorizationRequestContext>()
            .UseScopedHandler<CustomValidateAuthorizationRequest>()
            .SetOrder(int.MaxValue - 100_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateAuthorizationRequestContext context)
    {
        return default;
    }
}

public class CustomPopulateTokenClaims :
    IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private readonly UserManager<User> _userManager;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<CustomPopulateTokenClaims> _logger;

    public CustomPopulateTokenClaims(
        UserManager<User> userManager,
        IApplicationDbContext dbContext,
        ILogger<CustomPopulateTokenClaims> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleTokenRequestContext>()
            .UseScopedHandler<CustomPopulateTokenClaims>()
            .SetOrder(int.MaxValue - 100_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        var principal = context.Principal;
        if (principal is null) return;

        var userId = principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return;

        var identity = (ClaimsIdentity)principal.Identity!;

        identity.AddClaim(new Claim("fullName", user.FullName ?? ""));
        identity.AddClaim(new Claim("licenseNumber", user.LicenseNumber ?? ""));
        identity.AddClaim(new Claim("license_number", user.LicenseNumber ?? ""));

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, role));

        identity.AddClaim(new Claim("scope", "hishop:permissions"));

        identity.AddClaim(new Claim("amr", user.TwoFactorEnabled ? "mfa" : "pwd"));

        var claims = await _userManager.GetClaimsAsync(user);
        var facilityClaim = claims.FirstOrDefault(c => c.Type == "facility_id");
        if (facilityClaim is not null)
            identity.AddClaim(new Claim("facility_id", facilityClaim.Value));

        var permissions = await _dbContext.RolePermissions
            .Where(rp => roles.Contains(rp.Role.Name!))
            .Select(rp => rp.PermissionCode)
            .Distinct()
            .ToListAsync();

        if (permissions.Count > 0)
        {
            identity.AddClaim(new Claim("permissions", string.Join(",", permissions)));
        }
    }
}
