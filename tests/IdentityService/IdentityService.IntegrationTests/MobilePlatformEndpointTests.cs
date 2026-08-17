using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class MobilePlatformEndpointTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task App_policy_is_public_and_sync_requires_authentication()
    {
        var policy = await fixture.AnonymousClient.GetAsync(IdentityApiRoutes.MobileAppPolicy);
        var sync = await fixture.AnonymousClient.PostAsJsonAsync($"{IdentityApiRoutes.Mobile}/sync", new
        {
            idempotencyKey = "test-key",
            operation = "sync",
            payload = new { }
        });

        Assert.Equal(HttpStatusCode.OK, policy.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, sync.StatusCode);
    }

    [Fact]
    public async Task Push_token_registration_requires_authentication()
    {
        var response = await fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.MobilePushTokens, new
        {
            token = "",
            platform = "windows"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Notifications_requires_authentication()
    {
        var response = await fixture.AnonymousClient.GetAsync($"{IdentityApiRoutes.Mobile}/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Crash_report_rejects_missing_message()
    {
        var response = await fixture.AnonymousClient.PostAsJsonAsync($"{IdentityApiRoutes.Mobile}/crash-reports", new
        {
            message = "",
            stack = "at mobile.test()",
            route = "/home",
            appVersion = "1.2.3",
            platform = "ios",
            correlationId = "test-correlation"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rum_event_rejects_missing_name()
    {
        var response = await fixture.AnonymousClient.PostAsJsonAsync($"{IdentityApiRoutes.Mobile}/rum", new
        {
            name = "",
            durationMs = 125.5,
            route = "/login",
            appVersion = "1.2.3",
            platform = "android",
            metadata = new { source = "integration-test" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_mobile_registration_and_admin_revoke_follow_lifecycle()
    {
        using var session = await LoginAsync();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.MobilePushTokens, new { token = "token", platform = "windows" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.MobilePushTokens, new { token = $"push-{Guid.NewGuid():N}", platform = "android" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminMobile}/devices?page=1&pageSize=10")).StatusCode);

        var devices = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminMobile}/devices?page=1&pageSize=100");
        var body = await devices.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("items").EnumerateArray().First(item => item.GetProperty("platform").GetString() == "android").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminMobile}/devices/{id:D}/revoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminMobile}/devices/{id:D}/revoke", new { })).StatusCode);
    }

    [Fact]
    public async Task Authenticated_mobile_notifications_and_sync_enforce_contracts()
    {
        using var session = await LoginAsync();
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.Mobile}/notifications?page=0&pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.Mobile}/notifications/{Guid.NewGuid():D}/read", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.Mobile}/notifications/read-all", new { })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.Mobile}/sync", new
            {
                idempotencyKey = "invalid-schema",
                operation = "sync",
                payload = new { },
                schemaVersion = 2,
                conflictPolicy = "reject_on_stale"
            })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.Mobile}/sync", new
            {
                idempotencyKey = "patient-lww",
                operation = "update",
                payload = new { value = 1 },
                entityType = "patient",
                conflictPolicy = "last_write_wins"
            })).StatusCode);

        var key = $"sync-{Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.Accepted,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.Mobile}/sync", new
            {
                idempotencyKey = key,
                operation = "update",
                payload = new { value = 1 },
                entityType = "settings",
                conflictPolicy = "reject_on_stale"
            })).StatusCode);
        var duplicate = await session.PostWithCookiesAsync($"{IdentityApiRoutes.Mobile}/sync", new
        {
            idempotencyKey = key,
            operation = "update",
            payload = new { value = 1 },
            entityType = "settings",
            conflictPolicy = "reject_on_stale"
        });
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
    }

    [Fact]
    public async Task Admin_push_contracts_expose_summary_and_validate_payload()
    {
        using var session = await LoginAsync();
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminPushDeliverySummary}?hours=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPushNotifications, new { userId = "", title = "title", body = "body" })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPushNotifications, new { userId = "mobile-user", title = "test", body = "body" })).StatusCode);
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
