using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class UserEndpointCoverageTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task User_endpoints_require_authentication()
    {
        var get = await fixture.AnonymousClient.GetAsync(IdentityApiRoutes.Users);
        var create = await fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.Users, new
        {
            username = "anonymous-user",
            email = "anonymous-user@example.test",
            password = "Test@1234567",
            firstName = "Anonymous",
            lastName = "User"
        });

        Assert.True(get.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);
        Assert.True(create.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task User_list_validates_query_and_returns_items()
    {
        using var session = await LoginAsync();

        var list = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Users}?page=1&pageSize=10&sort=username:asc");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.GetProperty("items").ValueKind);

        var invalidPaging = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Users}?page=0&pageSize=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPaging.StatusCode);

        var longSearch = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Users}?search={new string('x', 101)}");
        Assert.Equal(HttpStatusCode.BadRequest, longSearch.StatusCode);
    }

    [Fact]
    public async Task User_detail_and_mutations_return_not_found_for_unknown_id()
    {
        using var session = await LoginAsync();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.User(id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PutWithCookiesAsync(IdentityApiRoutes.User(id), new { firstName = "Missing" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PutWithCookiesAsync($"{IdentityApiRoutes.User(id)}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PutWithCookiesAsync($"{IdentityApiRoutes.User(id)}/activate")).StatusCode);
    }

    [Fact]
    public async Task User_create_duplicate_and_update_validation_are_reported()
    {
        using var session = await LoginAsync();
        var duplicate = await session.PostWithCookiesAsync(IdentityApiRoutes.Users, new
        {
            username = "admin",
            email = "new-email@example.test",
            password = "Test@1234567",
            firstName = "Duplicate",
            lastName = "Admin"
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var created = await session.PostWithCookiesAsync(IdentityApiRoutes.Users, new
        {
            username = $"coverage-{Guid.NewGuid():N}",
            email = $"coverage-{Guid.NewGuid():N}@example.test",
            password = "Test@1234567",
            firstName = "Coverage",
            lastName = "User",
            role = "Admin"
        });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var user = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = user.GetProperty("id").GetGuid();

        var conflict = await session.PutWithCookiesAsync(IdentityApiRoutes.User(id), new
        {
            firstName = "Changed",
            concurrencyToken = "stale-token"
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task User_update_deactivate_and_activate_lifecycle_succeeds()
    {
        using var session = await LoginAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var created = await session.PostWithCookiesAsync(IdentityApiRoutes.Users, new
        {
            username = $"lifecycle-{suffix}",
            email = $"lifecycle-{suffix}@example.test",
            password = "Test@1234567",
            firstName = "Before",
            lastName = "Lifecycle",
            role = "Admin"
        });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var user = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = user.GetProperty("id").GetGuid();

        var update = await session.PutWithCookiesAsync(IdentityApiRoutes.User(id), new
        {
            firstName = "After",
            lastName = "Updated"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await session.PutWithCookiesAsync($"{IdentityApiRoutes.User(id)}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.PutWithCookiesAsync($"{IdentityApiRoutes.User(id)}/activate")).StatusCode);
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
