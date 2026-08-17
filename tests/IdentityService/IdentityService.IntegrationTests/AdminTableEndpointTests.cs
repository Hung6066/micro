using System.Net;
using System.Net.Http.Json;
using His.Hope.Contracts.Identity;
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

    private sealed record JobResponse(string JobId);
}
