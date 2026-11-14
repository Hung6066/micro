using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task Push_token_registration_accepts_ios_and_reprotects_existing_registration()
    {
        using var session = await LoginAsync();
        var token = $"ios-push-{Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.NoContent,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.MobilePushTokens,
                new { token, platform = "ios" })).StatusCode);
        // The second registration follows the existing-row path and must still
        // clear a previous revocation and refresh the protected token.
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.MobilePushTokens,
                new { token, platform = "ios" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.MobilePushTokens,
                new { token = new string('x', 4097), platform = "ios" })).StatusCode);
    }

    [Fact]
    public async Task Mobile_telemetry_accepts_valid_crash_and_rum_events()
    {
        var crash = await fixture.AnonymousClient.PostAsJsonAsync($"{IdentityApiRoutes.Mobile}/crash-reports", new
        {
            message = "integration crash",
            stack = "at Integration.Test()",
            route = "/home",
            appVersion = "1.2.3",
            platform = "ios",
            correlationId = Guid.NewGuid().ToString("N")
        });
        Assert.Equal(HttpStatusCode.NoContent, crash.StatusCode);

        var rum = await fixture.AnonymousClient.PostAsJsonAsync($"{IdentityApiRoutes.Mobile}/rum", new
        {
            name = "screen-load",
            durationMs = 125.5,
            route = "/dashboard",
            appVersion = "1.2.3",
            platform = "android",
            metadata = new { source = "integration-test" }
        });
        Assert.Equal(HttpStatusCode.NoContent, rum.StatusCode);
    }

    [Fact]
    public async Task Mobile_app_policy_returns_configured_values_and_defaults_latest_version()
    {
        var configuration = fixture.Services.GetRequiredService<IConfiguration>();
        var minimum = configuration["Mobile:AppPolicy:MinimumVersion"];
        var latest = configuration["Mobile:AppPolicy:LatestVersion"];
        var forceUpgrade = configuration["Mobile:AppPolicy:ForceUpgrade"];
        var storeUrl = configuration["Mobile:AppPolicy:StoreUrl"];
        var maintenance = configuration["Mobile:AppPolicy:Maintenance"];
        configuration["Mobile:AppPolicy:MinimumVersion"] = "9.2.0";
        configuration["Mobile:AppPolicy:LatestVersion"] = null;
        configuration["Mobile:AppPolicy:ForceUpgrade"] = "true";
        configuration["Mobile:AppPolicy:StoreUrl"] = "https://store.example/mobile";
        configuration["Mobile:AppPolicy:Maintenance"] = "true";

        try
        {
            var response = await fixture.AnonymousClient.GetAsync(IdentityApiRoutes.MobileAppPolicy);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("9.2.0", body.GetProperty("minimumVersion").GetString());
            Assert.Equal("9.2.0", body.GetProperty("latestVersion").GetString());
            Assert.True(body.GetProperty("forceUpgrade").GetBoolean());
            Assert.Equal("https://store.example/mobile", body.GetProperty("storeUrl").GetString());
            Assert.True(body.GetProperty("maintenance").GetBoolean());
        }
        finally
        {
            configuration["Mobile:AppPolicy:MinimumVersion"] = minimum;
            configuration["Mobile:AppPolicy:LatestVersion"] = latest;
            configuration["Mobile:AppPolicy:ForceUpgrade"] = forceUpgrade;
            configuration["Mobile:AppPolicy:StoreUrl"] = storeUrl;
            configuration["Mobile:AppPolicy:Maintenance"] = maintenance;
        }
    }

    [Fact]
    public async Task Mobile_sync_rejects_invalid_envelope_and_offline_patient_data_when_disabled()
    {
        using var session = await LoginAsync();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.Mobile}/sync", new
            {
                idempotencyKey = "",
                operation = "sync",
                payload = new { }
            })).StatusCode);

        var patient = await session.PostWithCookiesAsync($"{IdentityApiRoutes.Mobile}/sync", new
        {
            idempotencyKey = $"patient-disabled-{Guid.NewGuid():N}",
            operation = "update",
            payload = new { value = 1 },
            entityType = "patient-record",
            conflictPolicy = "reject_on_stale"
        });
        Assert.Equal(HttpStatusCode.Conflict, patient.StatusCode);
        Assert.Contains("offline_patient_data_disabled", await patient.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mobile_attestation_forbids_another_user_and_fails_closed_for_provider_or_unconfigured_policy()
    {
        using var session = await LoginAsync();
        var request = new
        {
            userId = Guid.NewGuid(),
            deviceId = "attestation-device",
            provider = "advanced-compliance",
            signals = new Dictionary<string, bool> { ["managed"] = true },
            observedAt = DateTime.UtcNow,
            replayNonce = Guid.NewGuid().ToString("N")
        };
        Assert.Equal(HttpStatusCode.Forbidden,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.Mobile}/attestation", request)).StatusCode);

        var unconfigured = await session.PostWithCookiesAsync($"{IdentityApiRoutes.Mobile}/attestation", new
        {
            userId = IdentityTestData.AdminId,
            deviceId = "unconfigured-device",
            provider = "advanced-compliance",
            signals = new Dictionary<string, bool> { ["managed"] = true },
            observedAt = DateTime.UtcNow,
            facilityId = $"missing-policy-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.Accepted, unconfigured.StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await session.PutWithCookiesAsync(IdentityApiRoutes.AdminDevicePosturePolicy, new
            {
                mode = "observe",
                providers = new[] { "advanced-compliance" },
                evidenceTtlSeconds = 900,
                requiredSignals = Array.Empty<string>()
            })).StatusCode);

        var invalidProvider = new
        {
            userId = IdentityTestData.AdminId,
            deviceId = "attestation-device",
            provider = "not-enabled-provider",
            signals = new Dictionary<string, bool> { ["managed"] = true },
            observedAt = DateTime.UtcNow,
            replayNonce = Guid.NewGuid().ToString("N")
        };
        var invalid = await session.PostWithCookiesAsync($"{IdentityApiRoutes.Mobile}/attestation", invalidProvider);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
