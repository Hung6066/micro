using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class RoleEndpointLifecycleTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Role_create_rejects_permissions_outside_server_catalog()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsAdminAsync()).StatusCode);

        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.Roles, new
        {
            name = $"invalid-permission-role-{Guid.NewGuid():N}",
            description = "Unknown permission must never be accepted from the client",
            permissions = new[] { "browser.admin", "not-in-server-catalog" },
            owner = "identity-service"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Role_reads_reject_invalid_query_and_return_not_found_without_leaking_data()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsAdminAsync()).StatusCode);

        var invalidSort = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Roles}?sort=not-a-server-field");
        var oversizedSearch = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Roles}?search={new string('x', 101)}");
        var missingRole = await session.GetWithCookiesAsync(IdentityApiRoutes.Role(Guid.NewGuid()));
        var missingVersions = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Role(Guid.NewGuid())}/versions");

        Assert.Equal(HttpStatusCode.BadRequest, invalidSort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, oversizedSearch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingRole.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingVersions.StatusCode);
    }

    [Fact]
    public async Task Role_publish_and_rollback_return_not_found_for_unknown_role()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsAdminAsync()).StatusCode);
        var id = Guid.NewGuid();

        var publish = await session.PostWithCookiesAsync($"{IdentityApiRoutes.Role(id)}/publish", new { });
        var rollback = await session.PostWithCookiesAsync($"{IdentityApiRoutes.Role(id)}/rollback", new { });

        Assert.Equal(HttpStatusCode.NotFound, publish.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, rollback.StatusCode);
    }

    [Fact]
    public async Task Role_catalog_reads_and_query_bounds_follow_the_server_contract()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsAdminAsync()).StatusCode);

        var invalidPage = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Roles}?page=0");
        var invalidPageSize = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Roles}?pageSize=1001");
        var permissions = await session.GetWithCookiesAsync(IdentityApiRoutes.Permissions);
        var owners = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Auth}/role-owners");

        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageSize.StatusCode);
        Assert.Equal(HttpStatusCode.OK, permissions.StatusCode);
        Assert.Equal(HttpStatusCode.OK, owners.StatusCode);
    }

    [Fact]
    public async Task Role_update_and_delete_return_not_found_for_unknown_role()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsAdminAsync()).StatusCode);
        var id = Guid.NewGuid();

        var update = await session.PutWithCookiesAsync(IdentityApiRoutes.Role(id), new
        {
            name = "missing-role-update",
            description = "Unknown role update",
            permissions = Array.Empty<string>(),
            owner = "identity-service"
        });
        var delete = await session.DeleteWithCookiesAsync(IdentityApiRoutes.Role(id));

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Retired_and_system_roles_are_not_publishable_or_rollbackable()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsAdminAsync()).StatusCode);
        var create = await session.PostWithCookiesAsync(IdentityApiRoutes.Roles, new
        {
            name = $"integration-role-state-{Guid.NewGuid():N}",
            description = "Role state branch coverage",
            permissions = Array.Empty<string>(),
            owner = "identity-service"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var role = await db.Roles.SingleAsync(item => item.Id == id);
            role.LifecycleStatus = "retired";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.Role(id)}/publish", new { })).StatusCode);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var role = await db.Roles.SingleAsync(item => item.Id == id);
            role.IsSystem = true;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.Role(id)}/rollback", new { })).StatusCode);
    }

    [Fact]
    public async Task Role_delete_rejects_roles_that_are_still_assigned_to_a_user()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsAdminAsync()).StatusCode);
        var create = await session.PostWithCookiesAsync(IdentityApiRoutes.Roles, new
        {
            name = $"integration-role-assigned-{Guid.NewGuid():N}",
            description = "Assigned role delete branch coverage",
            permissions = Array.Empty<string>(),
            owner = "identity-service"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid> { UserId = IdentityTestData.AdminId, RoleId = id });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.DeleteWithCookiesAsync(IdentityApiRoutes.Role(id))).StatusCode);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var link = await db.Set<IdentityUserRole<Guid>>().SingleAsync(item => item.UserId == IdentityTestData.AdminId && item.RoleId == id);
            db.Set<IdentityUserRole<Guid>>().Remove(link);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.DeleteWithCookiesAsync(IdentityApiRoutes.Role(id))).StatusCode);
    }

    [Fact]
    public async Task System_role_delete_is_rejected_as_immutable()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsAdminAsync()).StatusCode);

        var roles = await session.GetWithCookiesAsync(IdentityApiRoutes.Roles);
        Assert.Equal(HttpStatusCode.OK, roles.StatusCode);
        var document = JsonDocument.Parse(await roles.Content.ReadAsStringAsync()).RootElement;
        var systemRole = document.GetProperty("items").EnumerateArray()
            .First(item => item.TryGetProperty("isSystem", out var flag) && flag.GetBoolean());
        var id = systemRole.GetProperty("id").GetGuid();

        var response = await session.DeleteWithCookiesAsync(IdentityApiRoutes.Role(id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Role_create_update_versions_publish_and_delete_lifecycle_is_enforced()
    {
        using var session = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsAdminAsync()).StatusCode);

        var invalid = await session.PostWithCookiesAsync(IdentityApiRoutes.Roles, new
        {
            name = "invalid-owner-role",
            description = "invalid owner",
            permissions = Array.Empty<string>(),
            owner = "not-in-catalog"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var name = $"integration-role-{Guid.NewGuid():N}";
        var create = await session.PostWithCookiesAsync(IdentityApiRoutes.Roles, new
        {
            name,
            description = "Integration role lifecycle",
            permissions = Array.Empty<string>(),
            owner = "identity-service"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement;
        var id = created.GetProperty("id").GetGuid();
        var token = created.TryGetProperty("concurrencyToken", out var tokenElement) ? tokenElement.GetString() : null;

        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.Role(id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync($"{IdentityApiRoutes.Role(id)}/versions")).StatusCode);

        var update = await session.PutWithCookiesAsync(IdentityApiRoutes.Role(id), new
        {
            name = name + "-updated",
            description = "Updated integration role",
            permissions = Array.Empty<string>(),
            concurrencyToken = token,
            owner = "identity-service"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var publish = await session.PostWithCookiesAsync($"{IdentityApiRoutes.Role(id)}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var delete = await session.DeleteWithCookiesAsync(IdentityApiRoutes.Role(id));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await session.GetWithCookiesAsync(IdentityApiRoutes.Role(id))).StatusCode);
    }
}
