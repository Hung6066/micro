using His.Hope.IdentityService.Application.DTOs;
using His.Hope.Contracts;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Contracts.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using His.Hope.Infrastructure.Audit;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
namespace His.Hope.IdentityService.Api.Endpoints;

public static class ClientEndpoints
{
    public static RouteGroupBuilder MapClientEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetClients).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminClientsRead);
        group.MapGet("/{id}", GetClientById).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminClientsRead);
        group.MapPost("/", CreateClient).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminClientsWrite);
        group.MapPut("/{id}", UpdateClient).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminClientsWrite);
        group.MapDelete("/{id}", DeleteClient).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminClientsWrite);
        group.MapPost("/{id}/rotate-secret", RotateSecret).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminClientsWrite);
        group.MapGet("/{id}/onboarding", GetOnboarding).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminClientsRead);
        return group;
    }

    public static IEndpointRouteBuilder MapDynamicClientRegistration(this IEndpointRouteBuilder app)
    {
        app.MapPost(IdentityApiRoutes.OidcRegister, RegisterDynamicClient).AllowAnonymous();
        return app;
    }

    private static async Task<Results<Ok<PagedResult<ClientResponse>>, ValidationProblem>> GetClients(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? sort = null,
        IdentityDbContext db = null!,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["pageSize"] = ["pageSize must be between 1 and 100 and page must be at least 1."] });
        if (search?.Length > 100)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["search"] = ["Search must be 100 characters or fewer."] });

        var query = db.OpenIddictApplications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => (a.ClientId ?? "").Contains(term) || (a.DisplayName ?? "").Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var sortParts = sort?.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var descending = sortParts?.Length > 1 && string.Equals(sortParts[1], "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortParts?.FirstOrDefault()?.ToLowerInvariant(), descending) switch
        {
            ("displayname", false) => query.OrderBy(a => a.DisplayName),
            ("displayname", true) => query.OrderByDescending(a => a.DisplayName),
            ("clientid", true) => query.OrderByDescending(a => a.ClientId),
            _ => query.OrderBy(a => a.ClientId)
        };

        var clients = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var response = clients.Select(a => new ClientResponse(
            Id: a.Id.ToString(),
            ClientId: a.ClientId ?? "",
            DisplayName: a.DisplayName ?? "",
            Type: a.ClientType ?? "public",
            GrantTypes: ParseGrantTypes(a.Permissions),
            RedirectUris: ParseUris(a.RedirectUris),
            PostLogoutRedirectUris: ParseUris(a.PostLogoutRedirectUris),
            Scopes: ParseScopes(a.Permissions),
            IsActive: true,
            FacilityId: null,
            CreatedAt: DateTime.UtcNow,
            LastUsedAt: null,
            ConcurrencyToken: a.ConcurrencyToken
        )).ToList();

        return TypedResults.Ok(new PagedResult<ClientResponse>(response, totalCount, page, pageSize));
    }

    private static async Task<Results<Ok<ClientResponse>, NotFound>> GetClientById(
        string id, IdentityDbContext db, CancellationToken ct)
    {
        var client = await db.OpenIddictApplications
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (client is null) return TypedResults.NotFound();

        return TypedResults.Ok(new ClientResponse(
            Id: client.Id.ToString(),
            ClientId: client.ClientId ?? "",
            DisplayName: client.DisplayName ?? "",
            Type: client.ClientType ?? "public",
            GrantTypes: ParseGrantTypes(client.Permissions),
            RedirectUris: ParseUris(client.RedirectUris),
            PostLogoutRedirectUris: ParseUris(client.PostLogoutRedirectUris),
            Scopes: ParseScopes(client.Permissions),
            IsActive: true,
            FacilityId: null,
            CreatedAt: DateTime.UtcNow,
            LastUsedAt: null,
            ConcurrencyToken: client.ConcurrencyToken
        ));
    }

    private static async Task<Results<Created<ClientSecretResponse>, ProblemHttpResult>> CreateClient(
        CreateClientRequest request,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        IAuditService audit,
        HttpContext http,
        CancellationToken ct)
    {
        if (await appManager.FindByClientIdAsync(request.ClientId, ct) is not null)
            return TypedResults.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.Conflict });

        var normalizedType = request.Type.Trim().ToLowerInvariant();
        if (normalizedType is not ("public" or "confidential"))
            return TypedResults.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.Validation });

        var isConfidential = normalizedType == "confidential";
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = request.ClientId,
            DisplayName = request.DisplayName,
            ClientType = isConfidential
                ? OpenIddictConstants.ClientTypes.Confidential
                : OpenIddictConstants.ClientTypes.Public,
        };

        if (!string.IsNullOrWhiteSpace(request.Jwks))
            descriptor.JsonWebKeySet = new JsonWebKeySet(request.Jwks);

        foreach (var perm in BuildPermissions(request))
            descriptor.Permissions.Add(perm);

        if (request.RedirectUris is not null)
            foreach (var uri in request.RedirectUris)
                descriptor.RedirectUris.Add(new Uri(uri));

        if (request.PostLogoutRedirectUris is not null)
            foreach (var uri in request.PostLogoutRedirectUris)
                descriptor.PostLogoutRedirectUris.Add(new Uri(uri));

        string? secret = null;
        if (isConfidential)
        {
            secret = vaultStore.GenerateSecret(request.ClientId);
            descriptor.ClientSecret = secret;
            await vaultStore.StoreSecretAsync(request.ClientId, secret, ct);
        }

        await appManager.CreateAsync(descriptor, ct);
        await AdminAudit.LogAsync(audit, http, "CREATE", "Client", request.ClientId, ct);

        return TypedResults.Created($"/api/v1/admin/clients/{request.ClientId}",
            new ClientSecretResponse(request.ClientId, secret ?? "",
                secret is not null ? "Client secret generated. Store it securely - it will not be shown again." : "",
                secret is not null ? "client_secret_basic" : "none"));
    }

    private static async Task<Results<Ok<ClientResponse>, NotFound, ProblemHttpResult>> UpdateClient(
        string id, UpdateClientRequest request,
        IdentityDbContext db, IOpenIddictApplicationManager appManager, HttpRequest httpRequest,
        IAuditService audit, HttpContext http, CancellationToken ct)
    {
        var client = await db.OpenIddictApplications
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (client is null) return TypedResults.NotFound();

        var expectedToken = request.ConcurrencyToken
            ?? httpRequest.Headers.IfMatch.FirstOrDefault()?.Trim('"');
        if (!string.IsNullOrWhiteSpace(expectedToken) &&
            !string.Equals(expectedToken, client.ConcurrencyToken, StringComparison.Ordinal))
            return TypedResults.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.ConcurrencyConflict });

        if (request.DisplayName is not null)
            client.DisplayName = request.DisplayName;

        client.ConcurrencyToken = Guid.NewGuid().ToString("N");

        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAsync(audit, http, "UPDATE", "Client", id, ct);

        return TypedResults.Ok(new ClientResponse(
            Id: client.Id.ToString(),
            ClientId: client.ClientId ?? "",
            DisplayName: client.DisplayName ?? "",
            Type: client.ClientType ?? "public",
            GrantTypes: ParseGrantTypes(client.Permissions),
            RedirectUris: ParseUris(client.RedirectUris),
            PostLogoutRedirectUris: ParseUris(client.PostLogoutRedirectUris),
            Scopes: ParseScopes(client.Permissions),
            IsActive: true,
            FacilityId: null,
            CreatedAt: DateTime.UtcNow,
            LastUsedAt: null,
            ConcurrencyToken: client.ConcurrencyToken
        ));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteClient(
        string id, IdentityDbContext db, IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore, IAuditService audit, HttpContext http, CancellationToken ct)
    {
        var client = await db.OpenIddictApplications
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (client is null) return TypedResults.NotFound();

        if (client.ClientType == OpenIddictConstants.ClientTypes.Confidential)
            await vaultStore.RevokeSecretAsync(client.ClientId ?? "", ct);

        await appManager.DeleteAsync(client, ct);
        await AdminAudit.LogAsync(audit, http, "DELETE", "Client", id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ClientSecretResponse>, NotFound>> RotateSecret(
        string id, IdentityDbContext db, IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore, IAuditService audit, HttpContext http, CancellationToken ct)
    {
        var client = await db.OpenIddictApplications
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (client is null) return TypedResults.NotFound();
        if (client.ClientType != OpenIddictConstants.ClientTypes.Confidential)
            return TypedResults.NotFound();

        var newSecret = vaultStore.GenerateSecret(client.ClientId!);
        await vaultStore.StoreSecretAsync(client.ClientId!, newSecret, ct);
        await appManager.UpdateAsync(client, newSecret, ct);
        await AdminAudit.LogAsync(audit, http, "ROTATE_SECRET", "Client", id, ct);

        return TypedResults.Ok(new ClientSecretResponse(client.ClientId!, newSecret,
            "Client secret rotated. Store it securely - it will not be shown again.", "client_secret_basic"));
    }

    private static async Task<Results<Ok<ClientOnboardingResponse>, NotFound>> GetOnboarding(
        string id, IdentityDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        var client = await db.OpenIddictApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (client is null) return TypedResults.NotFound();

        var issuer = (configuration["OpenIddict:Issuer"] ?? configuration["Oidc:Issuer"] ?? "http://localhost:8081").TrimEnd('/');
        return TypedResults.Ok(new ClientOnboardingResponse(
            client.ClientId ?? "",
            client.DisplayName ?? client.ClientId ?? "",
            issuer,
            $"{issuer}{IdentityApiRoutes.OidcAuthorize}",
            $"{issuer}{IdentityApiRoutes.OidcToken}",
            $"{issuer}{IdentityApiRoutes.OidcJwks}",
            ParseGrantTypes(client.Permissions).ToArray(),
            ParseScopes(client.Permissions).ToArray(),
            client.ClientType == OpenIddictConstants.ClientTypes.Confidential ? "client_secret_basic" : "none"));
    }

    private static async Task<Results<Created<DynamicClientRegistrationResponse>, ProblemHttpResult, UnauthorizedHttpResult, Conflict<string>>> RegisterDynamicClient(
        DynamicClientRegistrationRequest request,
        HttpContext httpContext,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        CancellationToken ct)
    {
        var configuredToken = configuration["OpenIddict:DynamicRegistrationToken"] ?? configuration["Oidc:DynamicRegistrationToken"];
        var suppliedToken = httpContext.Request.Headers["X-Registration-Token"].FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(configuredToken) ||
            (configuredToken.StartsWith("${", StringComparison.Ordinal) && configuredToken.EndsWith('}')))
            return TypedResults.Unauthorized();
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(configuredToken),
                System.Text.Encoding.UTF8.GetBytes(suppliedToken)))
            return TypedResults.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.ClientName) || request.RedirectUris is not { Length: > 0 })
            return TypedResults.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["errorCode"] = ApiErrorCodes.ClientRegistrationFieldsRequired });
        if (request.RedirectUris.Any(uri => !IsAllowedRedirectUri(uri, hostEnvironment.IsDevelopment())))
            return TypedResults.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["errorCode"] = ApiErrorCodes.InvalidRedirectUris });

        var clientId = $"partner_{Guid.NewGuid():N}";
        var authMethod = request.TokenEndpointAuthMethod?.Trim().ToLowerInvariant() ?? "client_secret_basic";
        var confidential = authMethod is "client_secret_basic" or "client_secret_post" or "private_key_jwt";
        if (authMethod is not ("none" or "client_secret_basic" or "client_secret_post" or "private_key_jwt"))
            return TypedResults.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["errorCode"] = ApiErrorCodes.UnsupportedTokenEndpointAuthMethod });

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = request.ClientName.Trim(),
            ClientType = confidential ? OpenIddictConstants.ClientTypes.Confidential : OpenIddictConstants.ClientTypes.Public
        };
        var grants = request.GrantTypes is { Length: > 0 } ? request.GrantTypes : ["authorization_code", "refresh_token"];
        var scopes = request.Scopes is { Length: > 0 } ? request.Scopes : ["openid", "profile", "email"];
        foreach (var grant in grants)
            foreach (var permission in BuildPermissions(new CreateClientRequest(clientId, request.ClientName, confidential ? "confidential" : "public", [grant], request.RedirectUris.ToList(), request.PostLogoutRedirectUris?.ToList(), scopes.ToList(), null, request.Jwks)))
                descriptor.Permissions.Add(permission);
        foreach (var uri in request.RedirectUris) descriptor.RedirectUris.Add(new Uri(uri));
        foreach (var uri in request.PostLogoutRedirectUris ?? []) descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        if (!string.IsNullOrWhiteSpace(request.Jwks)) descriptor.JsonWebKeySet = new JsonWebKeySet(request.Jwks);

        string? secret = null;
        if (confidential && authMethod.StartsWith("client_secret", StringComparison.Ordinal))
        {
            secret = vaultStore.GenerateSecret(clientId);
            descriptor.ClientSecret = secret;
            await vaultStore.StoreSecretAsync(clientId, secret, ct);
        }
        await appManager.CreateAsync(descriptor, ct);
        return TypedResults.Created($"{IdentityApiRoutes.OidcRegister}/{clientId}", new DynamicClientRegistrationResponse(
            clientId, secret, request.ClientName.Trim(), request.RedirectUris, grants, scopes, authMethod));
    }

    private static bool IsAllowedRedirectUri(string value, bool development)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Fragment.Length > 0 ||
            uri.UserInfo.Length > 0)
            return false;

        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return true;

        // HTTP redirect URIs are limited to loopback during local development.
        // Never permit clear-text callbacks for a deployed registration endpoint.
        return development &&
               uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> ParseGrantTypes(string? permissions)
    {
        if (string.IsNullOrEmpty(permissions)) return new();
        var result = new List<string>();
        if (permissions.Contains("authorization_code")) result.Add("authorization_code");
        if (permissions.Contains("client_credentials")) result.Add("client_credentials");
        if (permissions.Contains("refresh_token")) result.Add("refresh_token");
        return result;
    }

    private static List<string> ParseScopes(string? permissions)
    {
        if (string.IsNullOrEmpty(permissions)) return new();
        return permissions.Split('[', ']', ',', ' ')
            .Where(s => s.StartsWith("scope:") || s == "openid" || s == "email" || s == "profile" || s == "roles")
            .Select(s => s.StartsWith("scope:") ? s[6..] : s)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();
    }

    private static List<string> ParseUris(string? uris)
    {
        if (string.IsNullOrEmpty(uris)) return new();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(uris) ?? new();
        }
        catch { return new(); }
    }

    private static HashSet<string> BuildPermissions(CreateClientRequest request)
    {
        var perms = new HashSet<string>
        {
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.Endpoints.Introspection,
        };

        foreach (var grant in request.GrantTypes)
        {
            switch (grant)
            {
                case "authorization_code":
                    perms.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
                    perms.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
                    perms.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
                    break;
                case "client_credentials":
                    perms.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
                    break;
                case "refresh_token":
                    perms.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
                    break;
            }
        }

        foreach (var scope in request.Scopes)
        {
            perms.Add(scope switch
            {
                "openid" => OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                "email" => OpenIddictConstants.Permissions.Scopes.Email,
                "profile" => OpenIddictConstants.Permissions.Scopes.Profile,
                "roles" => OpenIddictConstants.Permissions.Scopes.Roles,
                _ => $"scope:{scope}"
            });
        }

        return perms;
    }
}
