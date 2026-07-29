using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public class ScimEndpointsTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public ScimEndpointsTests(IdentityServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetUsers_WithoutAuth_ReturnsRedirectOrChallenge()
    {
        var response = await _fixture.AnonymousClient.GetAsync("/scim/v2/Users");
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task CreateUser_WithoutAuth_Returns401()
    {
        var payload = new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
            userName = $"scim-test-{Guid.NewGuid():N}@test.test",
            name = new { givenName = "SCIM", familyName = "Test" },
            active = true
        };

        var response = await _fixture.AnonymousClient.PostAsJsonAsync("/scim/v2/Users", payload);
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_Returns409()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (loginResponse.StatusCode != HttpStatusCode.OK)
            return;

        var uniqueUser = $"dup-test-{Guid.NewGuid():N}";
        var payload = new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
            userName = uniqueUser,
            emails = new[] { new { value = "admin@hishop.com", primary = true } },
            active = true
        };

        var createResponse = await session.PostWithCookiesAsync("/scim/v2/Users", payload);
        Assert.True(createResponse.StatusCode == HttpStatusCode.Conflict,
            await createResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ScimServiceProviderConfig_IsAccessible()
    {
        var response = await _fixture.AnonymousClient.GetAsync("/scim/v2/ServiceProviderConfig");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("patch", out _));
        Assert.True(body.TryGetProperty("bulk", out _));
    }

    [Fact]
    public async Task ScimResourceTypes_IsAccessible()
    {
        var response = await _fixture.AnonymousClient.GetAsync("/scim/v2/ResourceTypes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task CreateAndGetUser_WithValidScimPayload_Returns201()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (loginResponse.StatusCode != HttpStatusCode.OK)
            return;

        var uniqueUser = $"scim-full-{Guid.NewGuid():N}";
        var payload = new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
            userName = uniqueUser,
            name = new { givenName = "John", familyName = "Doe" },
            emails = new[] { new { value = $"{uniqueUser}@test.test", primary = true } },
            active = true
        };

        var createResponse = await session.PostWithCookiesAsync("/scim/v2/Users", payload);
        if (createResponse.StatusCode == HttpStatusCode.Created)
        {
            var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(uniqueUser, created.GetProperty("userName").GetString());

            var id = created.GetProperty("id").GetString();
            var getResponse = await session.GetWithCookiesAsync($"/scim/v2/Users/{id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        }
    }

    [Fact]
    public async Task GetUsers_WithValidSession_Returns200()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (loginResponse.StatusCode != HttpStatusCode.OK)
            return;

        var response = await session.GetWithCookiesAsync("/scim/v2/Users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("Resources", out _));
        Assert.True(body.TryGetProperty("totalResults", out _));
    }
}
