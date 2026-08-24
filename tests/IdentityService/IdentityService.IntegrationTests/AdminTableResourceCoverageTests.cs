using System.Net;
using His.Hope.Contracts.Identity;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class AdminTableResourceCoverageTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Export_covers_roles_clients_and_audit_resources()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        foreach (var resource in new[] { "roles", "clients", "audit" })
        {
            var response = await session.PostWithCookiesAsync(
                $"{IdentityApiRoutes.AdminTables}/{resource}/export",
                new { format = "json", columns = Array.Empty<string>(), rowKeys = Array.Empty<string>(), query = new { }, maskSensitive = true });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task Async_export_job_and_missing_job_endpoints_are_deterministic()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var response = await session.PostWithCookiesAsync(
            $"{IdentityApiRoutes.AdminTables}/roles/export",
            new { format = "csv", columns = Array.Empty<string>(), rowKeys = Array.Empty<string>(), query = new { }, async = true });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var missing = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/jobs/{missing}/events")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/jobs/{missing}/download")).StatusCode);
    }

    [Fact]
    public async Task Bulk_roles_and_clients_validate_action_and_empty_selection()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        foreach (var resource in new[] { "roles", "clients" })
        {
            var empty = await session.PostWithCookiesAsync(
                $"{IdentityApiRoutes.AdminTables}/{resource}/bulk",
                new { actionId = "delete", rowKeys = Array.Empty<string>(), query = new { } });
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

            var unsupported = await session.PostWithCookiesAsync(
                $"{IdentityApiRoutes.AdminTables}/{resource}/bulk",
                new { actionId = "activate", rowKeys = new[] { "not-an-id" }, query = new { } });
            Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        }
    }
}
