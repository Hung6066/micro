using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class SettingsEndpointTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public SettingsEndpointTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Settings_are_not_readable_without_an_authenticated_admin_session()
    {
        using var response = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.Settings);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });
    }

    [Fact]
    public async Task Single_setting_can_be_created_updated_and_read_back()
    {
        using var session = await LoginAsync();
        var key = $"integration.settings.{Guid.NewGuid():N}";

        using var create = await session.PutWithCookiesAsync(
            IdentityApiRoutes.Setting(key),
            new { value = "v1", description = "integration setting" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(key, created.GetProperty("key").GetString());
        Assert.Equal("v1", created.GetProperty("value").GetString());

        using var update = await session.PutWithCookiesAsync(
            IdentityApiRoutes.Setting(key),
            new { value = "v2", description = (string?)null });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("v2", updated.GetProperty("value").GetString());
        Assert.Equal("integration setting", updated.GetProperty("description").GetString());

        using var read = await session.GetWithCookiesAsync(IdentityApiRoutes.Setting(key));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var readBack = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("v2", readBack.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Bulk_settings_route_creates_and_updates_multiple_values()
    {
        using var session = await LoginAsync();
        var first = $"integration.bulk.{Guid.NewGuid():N}";
        var second = $"integration.bulk.{Guid.NewGuid():N}";

        using var create = await session.PutWithCookiesAsync(
            IdentityApiRoutes.Settings,
            new[] { new { key = first, value = "one" }, new { key = second, value = "two" } });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, created.ValueKind);
        Assert.Equal(2, created.GetArrayLength());

        using var update = await session.PutWithCookiesAsync(
            IdentityApiRoutes.Settings,
            new[] { new { key = first, value = "updated" } });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("updated", updated[0].GetProperty("value").GetString());
    }

    [Fact]
    public async Task Frontend_bulk_settings_route_accepts_named_settings_payload()
    {
        using var session = await LoginAsync();
        var key = $"integration.frontend-bulk.{Guid.NewGuid():N}";
        var route = $"{IdentityApiRoutes.Admin}{IdentityApiRoutes.SettingsSegment}/bulk";

        using var response = await session.PutWithCookiesAsync(
            route,
            new { settings = new[] { new { key, value = "frontend" } } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(key, body[0].GetProperty("key").GetString());
        Assert.Equal("frontend", body[0].GetProperty("value").GetString());
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = _fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
