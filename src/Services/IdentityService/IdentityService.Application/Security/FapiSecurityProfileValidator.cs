namespace His.Hope.IdentityService.Application.Security;

/// <summary>
/// Validates client registrations against the OpenID FAPI 2.0 Security Profile.
/// Intended for external regulated partners only; browser/internal SPA clients stay on baseline OIDC.
/// </summary>
public static class FapiSecurityProfileValidator
{
    public sealed record FapiClientRegistration(
        string ClientId,
        string TokenEndpointAuthMethod,
        bool IsConfidential,
        bool RequiresDpop,
        IReadOnlyList<string> RedirectUris,
        IReadOnlyList<string> GrantTypes);

    public sealed record FapiValidationResult(bool IsCompliant, IReadOnlyList<string> Violations);

    public static FapiValidationResult Validate(FapiClientRegistration registration)
    {
        var violations = new List<string>();

        if (!registration.IsConfidential)
            violations.Add("FAPI requires a confidential client.");

        if (registration.TokenEndpointAuthMethod is not ("private_key_jwt" or "tls_client_auth"))
            violations.Add("FAPI requires private_key_jwt or tls_client_auth at the token endpoint.");

        if (!registration.RequiresDpop)
            violations.Add("FAPI requires sender-constrained access tokens (DPoP or mTLS).");

        if (registration.GrantTypes.Any(grant => grant is "password" or "implicit"))
            violations.Add("FAPI forbids password and implicit grants.");

        if (registration.RedirectUris.Any(uri => uri.Contains('*', StringComparison.Ordinal)))
            violations.Add("FAPI requires exact redirect URI matching.");

        foreach (var uri in registration.RedirectUris)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            {
                violations.Add($"Invalid redirect URI: {uri}");
                continue;
            }

            if (parsed.Scheme != Uri.UriSchemeHttps)
                violations.Add($"FAPI redirect URI must use HTTPS: {uri}");
        }

        return new FapiValidationResult(violations.Count == 0, violations);
    }
}
