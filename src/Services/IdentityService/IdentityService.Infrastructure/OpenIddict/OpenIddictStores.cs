using His.Hope.Infrastructure.Audit;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.OpenIddict;

/// <summary>
/// Custom authorization store that logs consent grants to the audit trail.
/// </summary>
public class CustomAuthorizationStore :
    OpenIddict.EntityFrameworkCore.Stores.OpenIddictEntityFrameworkCoreAuthorizationStore<
        OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization,
        OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication,
        OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken,
        IdentityDbContext,
        Guid>
{
    private readonly IAuditService _audit;
    private readonly ILogger<CustomAuthorizationStore> _logger;

    public CustomAuthorizationStore(
        IdentityDbContext context,
        IAuditService audit,
        ILogger<CustomAuthorizationStore> logger)
        : base(context, Guid.NewGuid)
    {
        _audit = audit;
        _logger = logger;
    }

    public override async ValueTask CreateAsync(
        OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization authorization,
        CancellationToken ct)
    {
        await base.CreateAsync(authorization, ct);

        if (!string.IsNullOrEmpty(authorization.Subject))
        {
            await _audit.LogAsync(authorization.Subject, "SYSTEM",
                "OIDC_AUTHORIZE", "Authorization",
                authorization.Id.ToString(),
                $"Granted scopes: {authorization.Scopes}",
                ct: ct);
        }
    }
}

/// <summary>
/// Custom token store that logs token operations to the audit trail.
/// </summary>
public class CustomTokenStore :
    OpenIddict.EntityFrameworkCore.Stores.OpenIddictEntityFrameworkCoreTokenStore<
        OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken,
        OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication,
        OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization,
        IdentityDbContext,
        Guid>
{
    private readonly IAuditService _audit;
    private readonly ILogger<CustomTokenStore> _logger;

    public CustomTokenStore(
        IdentityDbContext context,
        IAuditService audit,
        ILogger<CustomTokenStore> logger)
        : base(context, Guid.NewGuid)
    {
        _audit = audit;
        _logger = logger;
    }

    public override async ValueTask CreateAsync(
        OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken token,
        CancellationToken ct)
    {
        await base.CreateAsync(token, ct);

        var action = token.Type switch
        {
            "authorization_code" => "CODE_ISSUED",
            "access_token" => "TOKEN_ISSUED",
            "refresh_token" => "REFRESH_ISSUED",
            _ => $"TOKEN_{token.Type?.ToUpperInvariant()}"
        };

        if (!string.IsNullOrEmpty(token.Subject))
        {
            await _audit.LogAsync(token.Subject, "SYSTEM",
                action, "Token",
                token.ReferenceId ?? token.Id.ToString(),
                $"Type: {token.Type}, App: {token.Application?.Id}",
                ct: ct);
        }
    }
}
