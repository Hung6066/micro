using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.IdentityService.Testing;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class AdminIncidentEndpointTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Session_listing_requires_admin_authentication()
    {
        var response = await fixture.AnonymousClient.GetAsync(IdentityApiRoutes.AdminUserSessions(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var centerResponse = await fixture.AnonymousClient.GetAsync(IdentityApiRoutes.AdminSessions);
        Assert.Equal(HttpStatusCode.Unauthorized, centerResponse.StatusCode);
    }

    [Fact]
    public async Task Credential_reset_requires_admin_authentication()
    {
        var response = await fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.AdminUserCredentialReset(Guid.NewGuid()), new
        {
            resetMfa = true,
            revokePasskeys = true,
            reason = "incident response"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Session_center_returns_server_backed_inventory_for_admin()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var response = await session.GetWithCookiesAsync(IdentityApiRoutes.AdminSessions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin-session-center.v1", body.GetProperty("schemaVersion").GetString());
        Assert.True(body.GetProperty("sessions").ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task Bulk_import_and_outbox_operations_require_admin_authentication()
    {
        using var content = new StringContent("username,email,firstname,lastname\nuser,user@example.com,Test,User", System.Text.Encoding.UTF8, "text/csv");
        var importResponse = await fixture.AnonymousClient.PostAsync(IdentityApiRoutes.AdminUsersBulkPreview, content);
        var reconcileResponse = await fixture.AnonymousClient.PostAsync(IdentityApiRoutes.AdminProvisioningReconcileScim, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        var ssfResponse = await fixture.AnonymousClient.PostAsync(IdentityApiRoutes.AdminSecuritySignalRetry(Guid.NewGuid()), new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, importResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reconcileResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, ssfResponse.StatusCode);
    }

    [Fact]
    public async Task Incident_controls_validate_reason_and_unknown_users_before_mutation()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var unknown = Guid.NewGuid();

        var userSessions = await session.GetWithCookiesAsync(IdentityApiRoutes.AdminUserSessions(unknown));
        var revokeMissingReason = await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.AdminUserSessions(unknown)}/missing-session");
        var revokeAllMissingReason = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminUserSessions(unknown)}/revoke-all", new { reason = " " });
        var resetMissingReason = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminUserCredentialReset(unknown), new
        {
            resetMfa = true,
            revokePasskeys = false,
            reason = " "
        });
        var resetNoCredential = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminUserCredentialReset(unknown), new
        {
            resetMfa = false,
            revokePasskeys = false,
            reason = "incident review"
        });
        var resetUnknown = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminUserCredentialReset(unknown), new
        {
            resetMfa = true,
            revokePasskeys = true,
            reason = "incident review"
        });

        Assert.Equal(HttpStatusCode.NotFound, userSessions.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, revokeMissingReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, revokeAllMissingReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, resetMissingReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, resetNoCredential.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resetUnknown.StatusCode);
    }

    [Fact]
    public async Task Incident_controls_return_not_found_for_unknown_resources_when_reason_is_valid()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsAdminAsync()).StatusCode);
        var unknown = Guid.NewGuid();

        var revokeSession = await session.DeleteWithCookiesAsync(
            $"{IdentityApiRoutes.AdminUserSessions(unknown)}/missing-session?reason=incident-review");
        var revokeAll = await session.PostWithCookiesAsync(
            $"{IdentityApiRoutes.AdminUserSessions(unknown)}/revoke-all",
            new { reason = "incident-review" });

        Assert.Equal(HttpStatusCode.NotFound, revokeSession.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, revokeAll.StatusCode);
    }

    [Fact]
    public async Task Incident_controls_can_revoke_all_sessions_and_reset_credentials_for_an_admin()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);

        var reset = await session.PostWithCookiesAsync(
            IdentityApiRoutes.AdminUserCredentialReset(IdentityTestData.AdminId),
            new { resetMfa = true, revokePasskeys = true, reason = "automated incident regression" });
        var resetMfaOnly = await session.PostWithCookiesAsync(
            IdentityApiRoutes.AdminUserCredentialReset(IdentityTestData.AdminId),
            new { resetMfa = true, revokePasskeys = false, reason = "automated incident mfa regression" });
        var resetPasskeysOnly = await session.PostWithCookiesAsync(
            IdentityApiRoutes.AdminUserCredentialReset(IdentityTestData.AdminId),
            new { resetMfa = false, revokePasskeys = true, reason = "automated incident passkey regression" });
        var revokeAll = await session.PostWithCookiesAsync(
            $"{IdentityApiRoutes.AdminUserSessions(IdentityTestData.AdminId)}/revoke-all",
            new { reason = "automated incident regression" });

        Assert.Equal(HttpStatusCode.OK, revokeAll.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resetMfaOnly.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resetPasskeysOnly.StatusCode);
        using var revokeBody = JsonDocument.Parse(await revokeAll.Content.ReadAsStringAsync());
        using var resetBody = JsonDocument.Parse(await reset.Content.ReadAsStringAsync());
        Assert.Equal(IdentityTestData.AdminId, revokeBody.RootElement.GetProperty("userId").GetGuid());
        Assert.True(revokeBody.RootElement.TryGetProperty("revokedSessions", out var revoked));
        Assert.True(revoked.GetInt32() >= 0);
        Assert.Equal(IdentityTestData.AdminId, resetBody.RootElement.GetProperty("userId").GetGuid());
        Assert.True(resetBody.RootElement.TryGetProperty("removedMfa", out var removedMfa));
        Assert.True(resetBody.RootElement.TryGetProperty("removedPasskeys", out var removedPasskeys));
        Assert.True(resetBody.RootElement.GetProperty("tokensRevoked").GetBoolean());
        Assert.True(removedMfa.GetInt32() >= 0);
        Assert.True(removedPasskeys.GetInt32() >= 0);
    }
}
