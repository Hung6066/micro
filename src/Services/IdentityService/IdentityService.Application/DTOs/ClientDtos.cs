namespace His.Hope.IdentityService.Application.DTOs;

public record CreateClientRequest(
    string ClientId,
    string DisplayName,
    string Type,
    List<string> GrantTypes,
    List<string>? RedirectUris,
    List<string>? PostLogoutRedirectUris,
    List<string> Scopes,
    string? FacilityId,
    string? Jwks = null);

public record UpdateClientRequest(
    string? DisplayName,
    List<string>? GrantTypes,
    List<string>? RedirectUris,
    List<string>? PostLogoutRedirectUris,
    List<string>? Scopes,
    bool? IsActive,
    string? ConcurrencyToken = null);

public record ClientResponse(
    string Id,
    string ClientId,
    string DisplayName,
    string Type,
    List<string> GrantTypes,
    List<string> RedirectUris,
    List<string> PostLogoutRedirectUris,
    List<string> Scopes,
    bool IsActive,
    string? FacilityId,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    string? ConcurrencyToken = null);

public record ClientSecretResponse(
    string ClientId,
    string ClientSecret,
    string Message,
    string? TokenEndpointAuthMethod = null);

public record ClientOnboardingResponse(
    string ClientId,
    string DisplayName,
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string JwksUri,
    string[] GrantTypes,
    string[] Scopes,
    string TokenEndpointAuthMethod,
    string? ClientSecret = null);

public record DynamicClientRegistrationRequest(
    string ClientName,
    string[] RedirectUris,
    string[]? PostLogoutRedirectUris,
    string[]? GrantTypes,
    string[]? Scopes,
    string? TokenEndpointAuthMethod = null,
    string? Jwks = null);

public record DynamicClientRegistrationResponse(
    string ClientId,
    string? ClientSecret,
    string ClientName,
    string[] RedirectUris,
    string[] GrantTypes,
    string[] Scopes,
    string TokenEndpointAuthMethod);
