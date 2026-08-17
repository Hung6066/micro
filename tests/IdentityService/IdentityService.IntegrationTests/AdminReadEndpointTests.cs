using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using His.Hope.Contracts.Identity;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class AdminReadEndpointTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public AdminReadEndpointTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task User_list_requires_authentication()
    {
        var response = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.Users);

        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Admin_session_can_read_users_and_unknown_user_is_not_found()
    {
        using var session = await LoginAsync();

        var list = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Users}?page=1&pageSize=10&sort=username:asc");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out var items));
        Assert.True(items.ValueKind == JsonValueKind.Array);

        var missing = await session.GetWithCookiesAsync(IdentityApiRoutes.User(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task User_list_rejects_invalid_paging_without_querying_database()
    {
        using var session = await LoginAsync();

        var response = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Users}?page=0&pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_session_can_read_roles_and_unknown_role_is_not_found()
    {
        using var session = await LoginAsync();

        var list = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Roles}?page=1&pageSize=10&sort=name:asc");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out var items));
        Assert.True(items.ValueKind == JsonValueKind.Array);

        var missing = await session.GetWithCookiesAsync(IdentityApiRoutes.Role(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Admin_session_can_read_settings_and_missing_setting_is_not_found()
    {
        using var session = await LoginAsync();

        var list = await session.GetWithCookiesAsync(IdentityApiRoutes.Settings);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.ValueKind == JsonValueKind.Array);

        var missing = await session.GetWithCookiesAsync(IdentityApiRoutes.Setting("test.missing.setting"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = _fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
