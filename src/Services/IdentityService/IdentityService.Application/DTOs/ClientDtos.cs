namespace His.Hope.IdentityService.Application.DTOs;

public record CreateClientRequest(
    string ClientId,
    string DisplayName,
    string Type,
    List<string> GrantTypes,
    List<string>? RedirectUris,
    List<string>? PostLogoutRedirectUris,
    List<string> Scopes,
    string? FacilityId);

public record UpdateClientRequest(
    string? DisplayName,
    List<string>? GrantTypes,
    List<string>? RedirectUris,
    List<string>? PostLogoutRedirectUris,
    List<string>? Scopes,
    bool? IsActive);

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
    DateTime? LastUsedAt);

public record ClientListResponse(
    List<ClientResponse> Clients,
    int TotalCount);

public record ClientSecretResponse(
    string ClientId,
    string ClientSecret,
    string Message);
