using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace His.Hope.IdentityService.Application.OpenIddict;

/// <summary>Revokes every OpenIddict authorization and token issued to a subject.</summary>
public interface IOpenIddictSubjectRevoker
{
    /// <returns>The number of authorizations and tokens that were revoked.</returns>
    Task<int> RevokeAllAsync(Guid subjectUserId, CancellationToken cancellationToken);
}

/// <summary>
/// Used when a user leaves (deactivation) or loses a role-bearing group
/// membership. Revoking the authorizations stops refresh-token redemption and
/// revoking the tokens invalidates reference/refresh tokens, so OIDC relying
/// parties (Headscale, Grafana, Vault, Headplane) cannot keep a session alive
/// with grants that no longer exist.
/// </summary>
public sealed class OpenIddictSubjectRevoker : IOpenIddictSubjectRevoker
{
    private readonly IOpenIddictAuthorizationManager _authorizations;
    private readonly IOpenIddictTokenManager _tokens;
    private readonly ILogger<OpenIddictSubjectRevoker> _logger;

    public OpenIddictSubjectRevoker(
        IOpenIddictAuthorizationManager authorizations,
        IOpenIddictTokenManager tokens,
        ILogger<OpenIddictSubjectRevoker> logger)
    {
        _authorizations = authorizations;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<int> RevokeAllAsync(Guid subjectUserId, CancellationToken cancellationToken)
    {
        var subject = subjectUserId.ToString();

        // Materialize before revoking: the EF stores stream results, and
        // updating while the reader is still open is not supported by Npgsql.
        var authorizations = new List<object>();
        await foreach (var authorization in _authorizations.FindBySubjectAsync(subject, cancellationToken))
            authorizations.Add(authorization);
        var tokens = new List<object>();
        await foreach (var token in _tokens.FindBySubjectAsync(subject, cancellationToken))
            tokens.Add(token);

        var revoked = 0;
        foreach (var authorization in authorizations)
        {
            if (await _authorizations.TryRevokeAsync(authorization, cancellationToken))
                revoked++;
        }
        foreach (var token in tokens)
        {
            if (await _tokens.TryRevokeAsync(token, cancellationToken))
                revoked++;
        }

        _logger.LogInformation(
            "Revoked {RevokedCount} OpenIddict authorizations/tokens for subject {SubjectUserId}.",
            revoked, subjectUserId);
        return revoked;
    }
}
