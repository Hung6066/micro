using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class TableAnalysisEndpointTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Analysis_formulas_requires_admin_permission()
    {
        var response = await fixture.AnonymousClient.GetAsync($"{IdentityApiRoutes.AdminTables}/analysis/formulas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analysis_formulas_returns_only_approved_catalog_entries()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var response = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/analysis/formulas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("user-active-count-v1", body, StringComparison.Ordinal);
        Assert.Contains("role-system-count-v1", body, StringComparison.Ordinal);
        Assert.Contains("client-type-count-v1", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analysis_rejects_unsupported_operation_and_grouping()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var unsupported = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/analysis", new
        {
            operation = "delete",
            groupBy = "active"
        });
        var unsupportedGroup = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/analysis", new
        {
            operation = "aggregate",
            groupBy = "email"
        });

        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unsupportedGroup.StatusCode);
    }

    [Fact]
    public async Task Formula_analysis_requires_a_catalog_formula()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var missing = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/analysis", new
        {
            operation = "formula",
            groupBy = "active"
        });
        var unknown = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/analysis", new
        {
            operation = "formula",
            groupBy = "active",
            formulaId = "custom-sql"
        });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Approved_formula_analysis_supports_role_resource()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var response = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/roles/analysis", new
        {
            operation = "formula",
            groupBy = "isSystem",
            formulaId = "role-system-count-v1",
            detailLimit = 0
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("roles", payload.GetProperty("resource").GetString());
        Assert.Equal("role-system-count-v1", payload.GetProperty("formulaId").GetString());
    }

    [Fact]
    public async Task Analysis_requires_a_group_by_field()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var response = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/analysis", new
        {
            operation = "aggregate",
            groupBy = " "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task User_analysis_returns_grouped_rows_and_honors_detail_limit()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var response = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminTables}/users/analysis", new
        {
            operation = "aggregate",
            groupBy = "active",
            detailLimit = 1
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("users", payload.GetProperty("resource").GetString());
        Assert.Equal("aggregate", payload.GetProperty("operation").GetString());
        Assert.True(payload.GetProperty("rows").GetArrayLength() > 0);
        foreach (var row in payload.GetProperty("rows").EnumerateArray())
            Assert.True(row.GetProperty("items").GetArrayLength() <= 1);
    }
}
