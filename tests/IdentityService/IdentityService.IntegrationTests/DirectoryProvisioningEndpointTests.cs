using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class DirectoryProvisioningEndpointTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Provisioning_readiness_and_delivery_health_require_admin_and_return_contracts()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.AnonymousClient.GetAsync(IdentityApiRoutes.AdminProvisioningReadiness)).StatusCode);
        using var session = await LoginAsync();

        var readiness = await session.GetWithCookiesAsync(IdentityApiRoutes.AdminProvisioningReadiness);
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
        var readinessBody = await readiness.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("dry-run", readinessBody.GetProperty("mode").GetString());
        Assert.Equal(3, readinessBody.GetProperty("targets").GetArrayLength());

        var health = await session.GetWithCookiesAsync(IdentityApiRoutes.AdminProvisioningDeliveryHealth);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.True((await health.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deliveries").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Provisioning_queue_rejects_invalid_contract_values()
    {
        using var session = await LoginAsync();
        var route = IdentityApiRoutes.AdminProvisioningQueue;

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(route, new { target = "", operation = "create", resourceType = "User", resourceId = "id" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(route, new { target = "scim", operation = "upsert", resourceType = "User", resourceId = "id" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(route, new { target = "unknown", operation = "create", resourceType = "User", resourceId = "id" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(route, new { target = "scim", operation = "create", resourceType = "Device", resourceId = "id" })).StatusCode);
    }

    [Fact]
    public async Task Provisioning_queue_jobs_and_retry_follow_lifecycle()
    {
        using var session = await LoginAsync();
        var route = IdentityApiRoutes.AdminProvisioningQueue;
        var create = await session.PostWithCookiesAsync(route, new
        {
            target = "SCIM",
            operation = "create",
            resourceType = "Group",
            resourceId = $"group-{Guid.NewGuid():N}",
            payload = new { displayName = "integration-group" }
        });
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminProvisioningJobs)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminProvisioningJob(id))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminProvisioningJobRetry(id), new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminProvisioningJob(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminProvisioningJobRetry(Guid.NewGuid()), new { })).StatusCode);
    }

    [Fact]
    public async Task Provisioning_reconcile_rejects_unknown_target()
    {
        using var session = await LoginAsync();
        var response = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminProvisioningReconcile}/unknown", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Provisioning_readiness_reports_missing_and_ready_target_configuration()
    {
        var configuration = fixture.Services.GetRequiredService<IConfiguration>();
        var keys = new[]
        {
            "Provisioning:Scim:Enabled", "PROVISIONING_SCIM_BASE_URL", "PROVISIONING_SCIM_TOKEN_URL", "PROVISIONING_SCIM_CLIENT_ID",
            "Provisioning:Entra:Enabled", "Provisioning:Entra:BaseUrl", "Provisioning:Entra:TokenUrl", "Provisioning:Entra:ClientId",
            "Provisioning:GoogleWorkspace:Enabled", "Provisioning:GoogleWorkspace:BaseUrl", "Provisioning:GoogleWorkspace:TokenUrl", "Provisioning:GoogleWorkspace:ServiceAccountSecretId"
        };
        var previous = keys.ToDictionary(key => key, key => configuration[key]);
        try
        {
            configuration["Provisioning:Scim:Enabled"] = "true";
            configuration["PROVISIONING_SCIM_BASE_URL"] = "https://scim.example.test";
            configuration["PROVISIONING_SCIM_TOKEN_URL"] = "https://scim.example.test/token";
            configuration["PROVISIONING_SCIM_CLIENT_ID"] = "scim-client";
            configuration["Provisioning:Entra:Enabled"] = "true";
            configuration["Provisioning:Entra:BaseUrl"] = "http://entra.invalid";
            configuration["Provisioning:Entra:TokenUrl"] = "https://entra.example.test/token";
            configuration["Provisioning:Entra:ClientId"] = "entra-client";
            configuration["Provisioning:GoogleWorkspace:Enabled"] = "false";

            using var session = await LoginAsync();
            var response = await session.GetWithCookiesAsync(IdentityApiRoutes.AdminProvisioningReadiness);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var targets = body.GetProperty("targets").EnumerateArray().ToArray();
            Assert.Equal("ready_for_dry_run", targets.Single(x => x.GetProperty("target").GetString() == "scim").GetProperty("status").GetString());
            Assert.Equal("configuration_missing", targets.Single(x => x.GetProperty("target").GetString() == "entra").GetProperty("status").GetString());
            Assert.Equal("disabled", targets.Single(x => x.GetProperty("target").GetString() == "google-workspace").GetProperty("status").GetString());
        }
        finally
        {
            foreach (var pair in previous) configuration[pair.Key] = pair.Value;
        }
    }

    [Fact]
    public void Provisioning_readiness_helper_classifies_disabled_missing_and_ready_targets()
    {
        var readiness = typeof(His.Hope.IdentityService.Api.Endpoints.DirectoryProvisioningEndpoints)
            .GetMethod("Readiness", BindingFlags.NonPublic | BindingFlags.Static)!;
        static string Status(object value) => value.GetType().GetProperty("status")!.GetValue(value)!.ToString()!;

        Assert.Equal("disabled", Status(readiness.Invoke(null, ["scim", false, null, null, null])!));
        Assert.Equal("configuration_missing", Status(readiness.Invoke(null, ["scim", true, "http://scim.invalid", "https://scim.example/token", "client"])!));
        var ready = readiness.Invoke(null, ["scim", true, "https://scim.example", "https://scim.example/token", "client"])!;
        Assert.Equal("ready_for_dry_run", Status(ready));
        Assert.True((bool)ready.GetType().GetProperty("credentialConfigured")!.GetValue(ready)!);
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
