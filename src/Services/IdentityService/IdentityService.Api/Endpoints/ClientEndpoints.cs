using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
namespace His.Hope.IdentityService.Api.Endpoints;

public static class ClientEndpoints
{
    public static RouteGroupBuilder MapClientEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetClients);
        group.MapGet("/{id}", GetClientById);
        group.MapPost("/", CreateClient);
        group.MapPut("/{id}", UpdateClient);
        group.MapDelete("/{id}", DeleteClient);
        group.MapPost("/{id}/rotate-secret", RotateSecret);
        return group;
    }

    private static async Task<Results<Ok<ClientListResponse>, ProblemHttpResult>> GetClients(
        IdentityDbContext db, CancellationToken ct)
    {
        var clients = await db.OpenIddictApplications
            .OrderBy(a => a.ClientId)
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
            LastUsedAt: null
        )).ToList();

        return TypedResults.Ok(new ClientListResponse(response, response.Count));
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
            LastUsedAt: null
        ));
    }

    private static async Task<Results<Created<ClientSecretResponse>, ProblemHttpResult>> CreateClient(
        CreateClientRequest request,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        CancellationToken ct)
    {
        if (await appManager.FindByClientIdAsync(request.ClientId, ct) is not null)
            return TypedResults.Problem("Client ID already exists", statusCode: 409);

        var isConfidential = request.Type == "confidential";
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = request.ClientId,
            DisplayName = request.DisplayName,
            ClientType = isConfidential
                ? OpenIddictConstants.ClientTypes.Confidential
                : OpenIddictConstants.ClientTypes.Public,
        };

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

        return TypedResults.Created($"/api/v1/admin/clients/{request.ClientId}",
            new ClientSecretResponse(request.ClientId, secret ?? "",
                secret is not null ? "Client secret generated. Store it securely - it will not be shown again." : ""));
    }

    private static async Task<Results<Ok<ClientResponse>, NotFound, ProblemHttpResult>> UpdateClient(
        string id, UpdateClientRequest request,
        IdentityDbContext db, IOpenIddictApplicationManager appManager, CancellationToken ct)
    {
        var client = await db.OpenIddictApplications
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (client is null) return TypedResults.NotFound();

        if (request.DisplayName is not null)
            client.DisplayName = request.DisplayName;

        await db.SaveChangesAsync(ct);

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
            LastUsedAt: null
        ));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteClient(
        string id, IdentityDbContext db, IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore, CancellationToken ct)
    {
        var client = await db.OpenIddictApplications
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (client is null) return TypedResults.NotFound();

        if (client.ClientType == OpenIddictConstants.ClientTypes.Confidential)
            await vaultStore.RevokeSecretAsync(client.ClientId ?? "", ct);

        await appManager.DeleteAsync(client, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ClientSecretResponse>, NotFound>> RotateSecret(
        string id, IdentityDbContext db, IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore, CancellationToken ct)
    {
        var client = await db.OpenIddictApplications
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (client is null) return TypedResults.NotFound();
        if (client.ClientType != OpenIddictConstants.ClientTypes.Confidential)
            return TypedResults.NotFound();

        var newSecret = vaultStore.GenerateSecret(client.ClientId!);
        await vaultStore.StoreSecretAsync(client.ClientId!, newSecret, ct);
        await appManager.UpdateAsync(client, newSecret, ct);

        return TypedResults.Ok(new ClientSecretResponse(client.ClientId!, newSecret,
            "Client secret rotated. Store it securely - it will not be shown again."));
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
