using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class EndpointContractCoverageTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public EndpointContractCoverageTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Consent_grant_update_list_and_revoke_follow_the_user_contract()
    {
        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var clientId = $"consent-test-{Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.Consents)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.Consents, new
            {
                clientId,
                scopes = new[] { "openid", "profile" }
            })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.Consents, new
            {
                clientId,
                scopes = new[] { "openid" }
            })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.Consents}/{clientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.Consents}/{clientId}")).StatusCode);
    }

    [Fact]
    public async Task Consent_endpoints_require_authentication_for_every_mutation()
    {
        var list = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.Consents);
        var grant = await _fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.Consents, new
        {
            clientId = "anonymous-consent-client",
            scopes = new[] { "openid" }
        });
        var revoke = await _fixture.AnonymousClient.DeleteAsync($"{IdentityApiRoutes.Consents}/anonymous-consent-client");

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, grant.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, revoke.StatusCode);
    }

    [Fact]
    public async Task Consent_list_tolerates_legacy_or_corrupt_scope_json()
    {
        var consentId = Guid.NewGuid();
        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.ClientConsents.Add(new ClientConsent
            {
                Id = consentId,
                UserId = IdentityTestData.AdminId,
                ClientId = $"legacy-consent-{consentId:N}",
                Scopes = "not-json",
                GrantedAt = DateTime.UtcNow,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        try
        {
            using var session = _fixture.CreateSessionClient();
            Assert.Equal(HttpStatusCode.OK,
                (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
            using var response = await session.GetWithCookiesAsync(IdentityApiRoutes.Consents);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            var item = Assert.Single(items!, value => value.GetProperty("id").GetString() == consentId.ToString());
            Assert.Empty(item.GetProperty("scopes").EnumerateArray());
        }
        finally
        {
            await using var scope = _fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var consent = await db.ClientConsents.FindAsync(consentId);
            if (consent is not null)
            {
                db.ClientConsents.Remove(consent);
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task Table_views_validate_payload_and_support_save_list_delete()
    {
        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var route = $"{IdentityApiRoutes.AdminTables}/users/views/clinical_Default";

        Assert.Equal(HttpStatusCode.OK,
            (await session.PutWithCookiesAsync(route, new { payloadJson = "{\"columns\":[\"id\"]}" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/views")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PutWithCookiesAsync(route, new { payloadJson = "not-json" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/invalid%20resource/views")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await session.DeleteWithCookiesAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await session.DeleteWithCookiesAsync(route)).StatusCode);
    }

    [Fact]
    public async Task Security_signal_admin_status_and_outbox_are_protected_and_replay_is_not_found()
    {
        Assert.Contains(
            (await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.AdminSecuritySignalsStatus)).StatusCode,
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });

        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminSecuritySignalsStatus)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminSecuritySignalsOutbox)).StatusCode);
        using var replayResponse = await session.PostWithCookiesAsync(
            $"{IdentityApiRoutes.AdminSecuritySignalsOutbox}/{Guid.NewGuid():D}/retry", new { });
        var replayBody = await replayResponse.Content.ReadAsStringAsync();
        Assert.True(replayResponse.StatusCode == HttpStatusCode.NotFound,
            $"Unexpected replay response {(int)replayResponse.StatusCode}: {replayBody}");
    }

    [Fact]
    public async Task Applications_projections_expose_api_audiences_and_trusted_issuers_from_server_catalog()
    {
        Assert.Contains(
            (await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.IamApiAudiences)).StatusCode,
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });

        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.IamApiAudiences)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.IamTrustedIssuers)).StatusCode);
    }
}
