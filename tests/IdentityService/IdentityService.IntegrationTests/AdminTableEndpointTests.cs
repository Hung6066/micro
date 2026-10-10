using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class AdminTableEndpointTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Bulk_table_actions_validate_empty_and_unknown_actions()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var empty = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/bulk", new
        {
            actionId = "activate",
            rowKeys = Array.Empty<string>(),
            query = new { }
        });
        var unknown = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/bulk", new
        {
            actionId = "delete",
            rowKeys = new[] { Guid.NewGuid().ToString("D") },
            query = new { }
        });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task User_export_supports_csv_json_and_xlsx_formats()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var url = $"{IdentityApiRoutes.AdminTables}/users/export";

        foreach (var format in new[] { "csv", "json", "xlsx" })
        {
            var response = await session.PostWithCookiesAsync(url, new
            {
                format,
                columns = Array.Empty<string>(),
                rowKeys = Array.Empty<string>(),
                query = new { },
                maskSensitive = true
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task Async_user_bulk_action_creates_job_and_job_can_be_cancelled()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var response = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/bulk", new
        {
            actionId = "deactivate",
            rowKeys = new[] { Guid.NewGuid().ToString("D") },
            query = new { },
            async = true
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.JobId));

        var job = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/jobs/{payload.JobId}");
        Assert.Equal(HttpStatusCode.OK, job.StatusCode);
        var cancel = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/jobs/{payload.JobId}/cancel");
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
    }

    [Fact]
    public async Task Export_rejects_unsupported_format_and_unknown_resource()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var unsupported = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/export", new
        {
            format = "xml", columns = Array.Empty<string>(), rowKeys = Array.Empty<string>(), query = new { }
        });
        var unknown = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/unknown/export", new
        {
            format = "json", columns = Array.Empty<string>(), rowKeys = Array.Empty<string>(), query = new { }
        });

        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Bulk_actions_cover_role_and_client_validation_and_noop_rows()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var invalidRoles = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/roles/bulk", new
        {
            actionId = "activate",
            rowKeys = new[] { Guid.NewGuid().ToString("D") },
            query = new { }
        });
        var emptyClients = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/clients/bulk", new
        {
            actionId = "delete",
            rowKeys = Array.Empty<string>(),
            query = new { }
        });
        var unknownClient = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/clients/bulk", new
        {
            actionId = "delete",
            rowKeys = new[] { "missing-client-id" },
            query = new { }
        });

        Assert.Equal(HttpStatusCode.BadRequest, invalidRoles.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyClients.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknownClient.StatusCode);
        var result = await unknownClient.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, result.GetProperty("updatedCount").GetInt32());
    }

    [Fact]
    public async Task Bulk_role_delete_protects_system_and_assigned_roles_and_deletes_unassigned_role()
    {
        Guid unassignedRoleId;
        Guid assignedRoleId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var unassignedName = $"table-test-unassigned-{Guid.NewGuid():N}";
            var unassigned = await roleManager.CreateAsync(new Role
            {
                Name = unassignedName,
                Description = "Admin table integration test",
                IsSystem = false,
                CreatedAt = DateTime.UtcNow
            });
            Assert.True(unassigned.Succeeded);
            unassignedRoleId = (await roleManager.FindByNameAsync(unassignedName))!.Id;

            var assignedName = $"table-test-assigned-{Guid.NewGuid():N}";
            var assignedCreateResult = await roleManager.CreateAsync(new Role
            {
                Name = assignedName,
                Description = "Admin table assigned integration test",
                IsSystem = false,
                CreatedAt = DateTime.UtcNow
            });
            Assert.True(assignedCreateResult.Succeeded);
            var assignedEntity = await roleManager.FindByNameAsync(assignedName);
            assignedRoleId = assignedEntity!.Id;
            var admin = await userManager.FindByIdAsync(IdentityTestData.AdminId.ToString());
            Assert.NotNull(admin);
            Assert.True((await userManager.AddToRoleAsync(admin!, assignedName)).Succeeded);
        }

        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var system = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/roles/bulk", new
        {
            actionId = "delete", rowKeys = new[] { (await FindRoleIdAsync("Admin")).ToString("D") }, query = new { }
        });
        var assigned = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/roles/bulk", new
        {
            actionId = "delete", rowKeys = new[] { assignedRoleId.ToString("D") }, query = new { }
        });
        var deleted = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/roles/bulk", new
        {
            actionId = "delete", rowKeys = new[] { unassignedRoleId.ToString("D") }, query = new { }
        });

        Assert.Equal(HttpStatusCode.Conflict, system.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, assigned.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var deletedBody = await deleted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, deletedBody.GetProperty("updatedCount").GetInt32());
    }

    [Fact]
    public async Task Export_covers_roles_clients_audit_and_column_masking()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        foreach (var resource in new[] { "roles", "clients", "audit" })
        {
            var response = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/{resource}/export", new
            {
                format = "json",
                columns = resource == "roles" ? new[] { "name" } : Array.Empty<string>(),
                rowKeys = new[] { "not-a-guid", "" },
                query = new { },
                maskSensitive = true
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            _ = await response.Content.ReadFromJsonAsync<JsonElement>();
        }
    }

    [Fact]
    public async Task Export_supports_csv_and_xlsx_contracts_for_users()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var csv = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/export", new
        {
            format = "csv",
            columns = Array.Empty<string>(),
            rowKeys = Array.Empty<string>(),
            query = new { },
            maskSensitive = false
        });
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);

        var xlsx = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/export", new
        {
            format = "xlsx",
            columns = Array.Empty<string>(),
            rowKeys = Array.Empty<string>(),
            query = new { },
            maskSensitive = true
        });
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType?.MediaType);
        Assert.True((await xlsx.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    [Fact]
    public async Task Export_rejects_more_than_ten_thousand_rows_before_querying()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var response = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/export", new
        {
            format = "json",
            columns = Array.Empty<string>(),
            rowKeys = Enumerable.Range(0, 10001).Select(_ => Guid.NewGuid().ToString("D")).ToArray(),
            query = new { }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_job_operations_return_not_found()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var jobId = $"missing-{Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.NotFound,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/jobs/{jobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/jobs/{jobId}/events")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/jobs/{jobId}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/jobs/{jobId}/download")).StatusCode);
    }

    [Fact]
    public async Task Table_view_authorization_covers_each_supported_resource_and_rejects_unknown_resources()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.AnonymousClient.GetAsync($"{IdentityApiRoutes.AdminTables}/users/views")).StatusCode);

        using var session = await fixture.CreateAuthenticatedSessionAsync();
        foreach (var resource in new[] { "users", "roles", "clients" })
        {
            var name = $"authorization-{resource}-{Guid.NewGuid():N}";
            var route = $"{IdentityApiRoutes.AdminTables}/{resource}/views/{name}";
            Assert.Equal(HttpStatusCode.OK,
                (await session.PutWithCookiesAsync(route, new { payloadJson = "{\"columns\":[]}" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/{resource}/views")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent,
                (await session.DeleteWithCookiesAsync(route)).StatusCode);
        }

        var unknown = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/audit/views");
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        var malformed = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/%20/views");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    private async Task<Guid> FindRoleIdAsync(string name)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        return (await roleManager.FindByNameAsync(name))!.Id;
    }

    private sealed record JobResponse(string JobId);
}
