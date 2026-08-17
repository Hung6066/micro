using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Reflection;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.Contracts.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class ClientEndpointTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Admin_client_crud_and_onboarding_paths_are_exercised()
    {
        using var session = await LoginAsync();

        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminClients)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminClients}?page=0&pageSize=20")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminClients, new
            {
                clientId = "invalid-client-type",
                displayName = "Invalid",
                type = "unsupported",
                grantTypes = new[] { "authorization_code" },
                redirectUris = new[] { "https://app.example/callback" },
                scopes = new[] { "openid" }
            })).StatusCode);

        var clientId = $"integration-client-{Guid.NewGuid():N}";
        var create = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminClients, new
        {
            clientId,
            displayName = "Integration Client",
            type = "confidential",
            grantTypes = new[] { "authorization_code", "refresh_token" },
            redirectUris = new[] { "https://app.example/callback" },
            postLogoutRedirectUris = new[] { "https://app.example/logout" },
            scopes = new[] { "openid", "profile", "email" }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var list = await session.GetWithCookiesAsync(IdentityApiRoutes.AdminClients);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items").EnumerateArray()
            .First(value => value.GetProperty("clientId").GetString() == clientId);
        var id = item.GetProperty("id").GetString()!;

        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminClients}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminClients}/{id}/onboarding")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminClients}/{id}/rotate-secret")).StatusCode);
        var staleUpdate = await session.PutWithCookiesAsync($"{IdentityApiRoutes.AdminClients}/{id}", new
        {
            displayName = "Stale Update",
            concurrencyToken = item.TryGetProperty("concurrencyToken", out var token) ? token.GetString() : null
        });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

        var current = await (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminClients}/{id}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK,
            (await session.PutWithCookiesAsync($"{IdentityApiRoutes.AdminClients}/{id}", new
            {
                displayName = "Updated Integration Client",
                concurrencyToken = current.TryGetProperty("concurrencyToken", out var currentToken) ? currentToken.GetString() : null
            })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.AdminClients}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Dynamic_registration_requires_a_configured_registration_token()
    {
        var response = await fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.OidcRegister, new
        {
            clientName = "External client",
            redirectUris = new[] { "https://partner.example/callback" }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Client_redirect_uri_and_permission_parsers_fail_closed()
    {
        var type = typeof(ClientEndpoints);
        var allowed = type.GetMethod("IsAllowedRedirectUri", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.True((bool)allowed.Invoke(null, ["https://partner.example/callback", false])!);
        Assert.False((bool)allowed.Invoke(null, ["https://partner.example/callback#fragment", false])!);
        Assert.False((bool)allowed.Invoke(null, ["https://user:secret@partner.example/callback", false])!);
        Assert.False((bool)allowed.Invoke(null, ["http://partner.example/callback", true])!);
        Assert.True((bool)allowed.Invoke(null, ["http://localhost:5173/callback", true])!);

        var grantTypes = type.GetMethod("ParseGrantTypes", BindingFlags.NonPublic | BindingFlags.Static)!;
        var scopes = type.GetMethod("ParseScopes", BindingFlags.NonPublic | BindingFlags.Static)!;
        var uris = type.GetMethod("ParseUris", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Empty((System.Collections.IEnumerable)grantTypes.Invoke(null, [null])!);
        Assert.Equal(new[] { "authorization_code", "refresh_token" }, ((System.Collections.IEnumerable)grantTypes.Invoke(null, ["authorization_code refresh_token ignored"])!).Cast<string>());
        Assert.Contains("custom", ((System.Collections.IEnumerable)scopes.Invoke(null, ["scope:custom scope:custom openid roles"])!).Cast<string>());
        Assert.Empty((System.Collections.IEnumerable)uris.Invoke(null, ["not-json"])!);
    }

    [Fact]
    public async Task Dynamic_registration_rejects_invalid_redirects_and_auth_methods()
    {
        var configuration = fixture.Services.GetRequiredService<IConfiguration>();
        var previous = configuration["OpenIddict:DynamicRegistrationToken"];
        configuration["OpenIddict:DynamicRegistrationToken"] = "integration-registration-token";
        try
        {
            using var wrong = new HttpRequestMessage(HttpMethod.Post, IdentityApiRoutes.OidcRegister)
            {
                Content = JsonContent.Create(new { clientName = "Partner", redirectUris = new[] { "https://partner.example/callback" } })
            };
            wrong.Headers.Add("X-Registration-Token", "wrong");
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.AnonymousClient.SendAsync(wrong)).StatusCode);

            using var missing = new HttpRequestMessage(HttpMethod.Post, IdentityApiRoutes.OidcRegister)
            {
                Content = JsonContent.Create(new { clientName = "Partner", redirectUris = Array.Empty<string>() })
            };
            missing.Headers.Add("X-Registration-Token", "integration-registration-token");
            Assert.Equal(HttpStatusCode.BadRequest, (await fixture.AnonymousClient.SendAsync(missing)).StatusCode);

            using var insecure = new HttpRequestMessage(HttpMethod.Post, IdentityApiRoutes.OidcRegister)
            {
                Content = JsonContent.Create(new { clientName = "Partner", redirectUris = new[] { "http://partner.example/callback" } })
            };
            insecure.Headers.Add("X-Registration-Token", "integration-registration-token");
            Assert.Equal(HttpStatusCode.BadRequest, (await fixture.AnonymousClient.SendAsync(insecure)).StatusCode);

            using var unsupported = new HttpRequestMessage(HttpMethod.Post, IdentityApiRoutes.OidcRegister)
            {
                Content = JsonContent.Create(new { clientName = "Partner", redirectUris = new[] { "https://partner.example/callback" }, tokenEndpointAuthMethod = "unsupported" })
            };
            unsupported.Headers.Add("X-Registration-Token", "integration-registration-token");
            Assert.Equal(HttpStatusCode.BadRequest, (await fixture.AnonymousClient.SendAsync(unsupported)).StatusCode);
        }
        finally
        {
            configuration["OpenIddict:DynamicRegistrationToken"] = previous;
        }
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
