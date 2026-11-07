using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Testing;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class MtlsEndpointTests(IdentityServiceTestFixture fixture)
{
    private const string BindingsRoute = IdentityApiRoutes.AdminMtlsBindings;

    [Fact]
    public async Task Admin_binding_crud_exercises_validation_conflict_and_revocation_paths()
    {
        using var session = await LoginAsync();

        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(BindingsRoute)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(BindingsRoute, new
            {
                userId = "not-a-guid",
                thumbprint = ""
            })).StatusCode);

        var thumbprint = $"aa bb cc {Guid.NewGuid():N}";
        var create = await session.PostWithCookiesAsync(BindingsRoute, new
        {
            userId = IdentityTestData.AdminId,
            thumbprint,
            subject = "CN=integration-client",
            notAfter = DateTime.UtcNow.AddHours(1)
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(thumbprint.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant(),
            created.GetProperty("thumbprint").GetString());

        var duplicate = await session.PostWithCookiesAsync(BindingsRoute, new
        {
            userId = IdentityTestData.AdminId,
            thumbprint,
            notAfter = DateTime.UtcNow.AddHours(1)
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await session.DeleteWithCookiesAsync($"{BindingsRoute}/{Guid.NewGuid():D}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.DeleteWithCookiesAsync($"{BindingsRoute}/{id:D}")).StatusCode);

        var bindings = await (await session.GetWithCookiesAsync(BindingsRoute))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(bindings.EnumerateArray(), item =>
            item.GetProperty("id").GetGuid() == id &&
            item.GetProperty("status").GetString() == "revoked");
    }

    [Fact]
    public async Task Admin_binding_routes_require_human_admin_session()
    {
        var get = await fixture.AnonymousClient.GetAsync(BindingsRoute);
        Assert.True(get.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);
        var post = await fixture.AnonymousClient.PostAsJsonAsync(BindingsRoute, new
        {
            userId = IdentityTestData.AdminId,
            thumbprint = "AABBCC",
            notAfter = DateTime.UtcNow.AddHours(1)
        });
        Assert.True(post.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Binding_for_unassigned_user_is_forbidden_and_expired_binding_is_reported()
    {
        using var session = await LoginAsync();
        var forbidden = await session.PostWithCookiesAsync(BindingsRoute, new
        {
            userId = Guid.NewGuid(),
            thumbprint = $"expired-{Guid.NewGuid():N}",
            notAfter = DateTime.UtcNow.AddMinutes(-1)
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var expired = await session.PostWithCookiesAsync(BindingsRoute, new
        {
            userId = IdentityTestData.AdminId,
            thumbprint = $"expired-{Guid.NewGuid():N}",
            notAfter = DateTime.UtcNow.AddMinutes(-1)
        });
        Assert.Equal(HttpStatusCode.Created, expired.StatusCode);
        var body = await expired.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        var listed = await session.GetWithCookiesAsync(BindingsRoute);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var bindings = await listed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(bindings.EnumerateArray(), item =>
            item.GetProperty("id").GetGuid() == id && item.GetProperty("status").GetString() == "expired");
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
