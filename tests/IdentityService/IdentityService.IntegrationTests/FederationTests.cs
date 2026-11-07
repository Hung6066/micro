using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

 [Collection("IdentityServiceIntegration")]
public class FederationTests
{
    private readonly HttpClient _client;
    private readonly IdentityServiceTestFixture _fixture;

    public FederationTests(IdentityServiceTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.AnonymousClient;
    }

    [Fact]
    public async Task ExternalProvidersEndpoint_ReturnsProviders()
    {
        var response = await _client.GetAsync(IdentityApiRoutes.ExternalProviders);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("providers", body);
    }

    [Fact]
    public async Task ExternalLogin_Challenge_RedirectsToProvider()
    {
        var response = await _client.GetAsync(IdentityApiRoutes.ExternalLogin + "/Google");
        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect ||
            response.StatusCode == HttpStatusCode.Found ||
            response.StatusCode == HttpStatusCode.RedirectMethod);
    }

    [Fact]
    public async Task ExternalLogin_RejectsExternalReturnUrlBeforeChallenge()
    {
        var response = await _client.GetAsync(
            IdentityApiRoutes.ExternalLogin + "/Google?returnUrl=https%3A%2F%2Fevil.example%2Fsteal");
        Assert.True(response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.Found || response.StatusCode == HttpStatusCode.RedirectMethod);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.DoesNotContain("evil.example", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LinkedAccounts_RequiresAuth()
    {
        var response = await _client.GetAsync(IdentityApiRoutes.AccountLinkedAccounts);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_linked_accounts_can_list_and_reject_unknown_mutations()
    {
        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AccountLinkedAccounts)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.AccountLinkedAccounts}/UnknownProvider")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.Account}/link/UnknownProvider")).StatusCode);
    }

    [Fact]
    public async Task Account_linking_accepts_configured_provider_and_handles_callback_failures_safely()
    {
        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);

        var challenge = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Account}/link/Google");
        var unsupportedCallback = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Account}/link-callback/UnknownProvider");
        var failedCallback = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Account}/link-callback/Google");

        Assert.Contains(challenge.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.RedirectMethod });
        Assert.Equal(HttpStatusCode.Redirect, unsupportedCallback.StatusCode);
        Assert.Equal("/profile?error=unsupported_provider", unsupportedCallback.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.Redirect, failedCallback.StatusCode);
        Assert.Equal("/profile?error=link_failed", failedCallback.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Authenticated_linked_account_can_be_listed_and_unlinked_case_insensitively()
    {
        var providerKey = $"integration-link-{Guid.NewGuid():N}";
        var email = $"linked-account-{Guid.NewGuid():N}@test.test";
        const string password = "LinkingTest123!";
        await using var scope = _fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User { UserName = email, Email = email, FirstName = "Linked", LastName = "Account" };
        var create = await userManager.CreateAsync(user, password);
        Assert.True(create.Succeeded, string.Join("; ", create.Errors.Select(error => error.Description)));
        var add = await userManager.AddLoginAsync(user, new UserLoginInfo("Google", providerKey, "Google"));
        Assert.True(add.Succeeded, string.Join("; ", add.Errors.Select(error => error.Description)));

        try
        {
            using var session = _fixture.CreateSessionClient();
            Assert.Equal(HttpStatusCode.OK,
                (await session.LoginAsync(email, password)).StatusCode);

            var listed = await session.GetWithCookiesAsync(IdentityApiRoutes.AccountLinkedAccounts);
            Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            var accounts = await listed.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains(accounts.EnumerateArray(), item =>
                item.GetProperty("provider").GetString() == "Google" &&
                item.GetProperty("displayName").GetString() == "Google");

            Assert.Equal(HttpStatusCode.NoContent,
                (await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.AccountLinkedAccounts}/google")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.AccountLinkedAccounts}/Google")).StatusCode);
        }
        finally
        {
            await userManager.DeleteAsync(user);
        }
    }

    [Fact]
    public async Task LoginPage_UsesCspCompatiblePasskeyScriptAndHidesUnavailableSaml()
    {
        var response = await _client.GetAsync("/Account/Login?returnUrl=%2Fconnect%2Fauthorize");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(IdentityApiRoutes.IdentityLoginScript, body, StringComparison.Ordinal);
        Assert.DoesNotContain("addEventListener('click'", body, StringComparison.Ordinal);
        Assert.DoesNotContain(IdentityApiRoutes.FederationSamlLogin, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PasskeyRegistrationOptions_RequiresAuth()
    {
        var response = await _client.PostAsync(IdentityApiRoutes.PasskeyRegisterOptions, content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LdapLogin_IsFailClosedWhenFederationIsNotConfigured()
    {
        var response = await _client.PostAsJsonAsync(
            IdentityApiRoutes.LdapLogin,
            new { userName = "unknown", password = "invalid" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SamlLogin_IsNotAvailableWithoutIdpMetadata()
    {
        var response = await _client.GetAsync(IdentityApiRoutes.FederationSamlLogin);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Unauthorized });
    }
}
