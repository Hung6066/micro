using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
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
        var response = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.ScimUsers);
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

        var response = await _fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.ScimUsers, payload);
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task CreateUser_WithAdminSession_IsRejectedWithoutScimScope()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var uniqueUser = $"dup-test-{Guid.NewGuid():N}";
        var payload = new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
            userName = uniqueUser,
            emails = new[] { new { value = IdentityTestCredentials.Email, primary = true } },
            active = true
        };

        var createResponse = await session.PostWithCookiesAsync(IdentityApiRoutes.ScimUsers, payload);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task ScimServiceProviderConfig_IsAccessible()
    {
        var response = await _fixture.AnonymousClient.GetAsync($"{IdentityApiRoutes.ScimV2}/ServiceProviderConfig");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig", body.GetProperty("schemas")[0].GetString());
        Assert.True(body.TryGetProperty("patch", out _));
        Assert.True(body.TryGetProperty("bulk", out _));
        Assert.Equal(100, body.GetProperty("bulk").GetProperty("maxOperations").GetInt32());
        Assert.Equal(1048576, body.GetProperty("bulk").GetProperty("maxPayloadSize").GetInt32());
        Assert.True(body.GetProperty("filter").GetProperty("supported").GetBoolean());
        Assert.Equal(200, body.GetProperty("filter").GetProperty("maxResults").GetInt32());
        Assert.True(body.GetProperty("changePassword").GetProperty("supported").GetBoolean());
        Assert.False(body.GetProperty("sort").GetProperty("supported").GetBoolean());
        Assert.Equal("oauthbearertoken", body.GetProperty("authenticationSchemes")[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task ScimResourceTypes_IsAccessible()
    {
        var response = await _fixture.AnonymousClient.GetAsync($"{IdentityApiRoutes.ScimV2}/ResourceTypes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.ValueKind == JsonValueKind.Array);
        Assert.Contains(body.EnumerateArray(), item => item.GetProperty("id").GetString() == "User");
        Assert.Contains(body.EnumerateArray(), item => item.GetProperty("id").GetString() == "Group");
        var user = body.EnumerateArray().Single(item => item.GetProperty("id").GetString() == "User");
        Assert.Equal("/scim/v2/Users", user.GetProperty("endpoint").GetString());
        Assert.Equal("urn:ietf:params:scim:schemas:core:2.0:User", user.GetProperty("schema").GetString());
    }

    [Fact]
    public async Task CreateUser_WithAdminSession_IsForbiddenWithoutScimScope()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.ScimUsers, new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
            userName = $"scim-full-{Guid.NewGuid():N}",
            name = new { givenName = "John", familyName = "Doe" },
            emails = new[] { new { value = "scim@example.test", primary = true } },
            active = true
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithAdminSession_IsRejectedWithoutScimScope()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await session.GetWithCookiesAsync(IdentityApiRoutes.ScimUsers);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
