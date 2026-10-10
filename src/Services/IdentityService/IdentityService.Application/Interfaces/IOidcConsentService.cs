namespace His.Hope.IdentityService.Application.Interfaces;

public sealed record OidcConsentClient(
    string ClientId,
    string DisplayName,
    IReadOnlyList<string> RedirectUris);

public interface IOidcConsentService
{
    Task<OidcConsentClient?> GetClientAsync(string clientId, CancellationToken cancellationToken = default);

    Task SaveConsentAsync(
        Guid userId,
        string clientId,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default);
}
