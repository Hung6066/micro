using System.Net;
using System.Net.Http.Json;
using His.Hope.Contracts.Identity;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class UserEndpointBranchTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public UserEndpointBranchTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task UserEndpoints_require_authentication()
    {
        var response = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.AdminUsers);

        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task ListUsers_rejects_invalid_pagination_and_sort_contracts()
    {
        using var session = await LoginAsync();

        var badPage = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}?page=0&pageSize=20");
        var badSort = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}?sort=unsupported:asc");

        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode);
    }

    [Fact]
    public async Task ListUsers_rejects_oversized_search_and_role_filters()
    {
        using var session = await LoginAsync();
        var oversized = new string('x', 101);

        var search = await session.GetWithCookiesAsync(
            $"{IdentityApiRoutes.AdminUsers}?search={Uri.EscapeDataString(oversized)}");
        var role = await session.GetWithCookiesAsync(
            $"{IdentityApiRoutes.AdminUsers}?role={Uri.EscapeDataString(oversized)}");

        Assert.Equal(HttpStatusCode.BadRequest, search.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, role.StatusCode);
    }

    [Fact]
    public async Task ListUsers_accepts_supported_query_contract()
    {
        using var session = await LoginAsync();
        var response = await session.GetWithCookiesAsync(
            $"{IdentityApiRoutes.AdminUsers}?page=1&pageSize=1&sort=username:desc&isActive=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserMutations_return_not_found_for_unknown_user()
    {
        using var session = await LoginAsync();
        var id = Guid.NewGuid();

        var get = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}/{id}");
        var update = await session.PutWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}/{id}", new
        {
            firstName = "Missing",
            lastName = "User",
            email = $"missing-{id:N}@example.test",
            phoneNumber = (string?)null,
            role = (string?)null,
            isActive = true,
            concurrencyToken = (string?)null
        });
        var deactivate = await session.PutWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}/{id}/deactivate");
        var activate = await session.PutWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}/{id}/activate");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, activate.StatusCode);
    }

    [Fact]
    public async Task CreateUser_rejects_duplicate_identity()
    {
        using var session = await LoginAsync();
        var username = $"duplicate-{Guid.NewGuid():N}";
        var request = new
        {
            username,
            email = $"{username}@example.test",
            password = "NewUser-password!123",
            firstName = "Duplicate",
            lastName = "User",
            middleName = (string?)null,
            licenseNumber = (string?)null,
            specialty = (string?)null,
            phoneNumber = (string?)null,
            role = "Admin"
        };

        var first = await session.PostWithCookiesAsync(IdentityApiRoutes.Users, request);
        var duplicate = await session.PostWithCookiesAsync(IdentityApiRoutes.Users, request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = _fixture.CreateSessionClient();
        var response = await session.LoginAsAdminAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
