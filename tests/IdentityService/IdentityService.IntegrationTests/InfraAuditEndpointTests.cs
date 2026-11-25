using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Application.InfraAudit;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class InfraAuditEndpointTests(IdentityServiceTestFixture fixture)
{
    private static readonly string Root = $"{IdentityApiRoutes.AdminIam}/infra-audit";

    [Fact]
    public async Task Anonymous_callers_cannot_read_the_infrastructure_audit()
    {
        foreach (var route in new[] { $"{Root}/meta", $"{Root}/events", $"{Root}/export", $"{IdentityApiRoutes.Admin}/infra-audit/me/events" })
        {
            var response = await fixture.AnonymousClient.GetAsync(route);
            Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect, route);
        }
    }

    [Fact]
    public async Task Unconfigured_host_reports_meta_and_answers_503_instead_of_failing()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var meta = await session.GetWithCookiesAsync($"{Root}/meta");
        Assert.Equal(HttpStatusCode.OK, meta.StatusCode);
        var body = await meta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("configured").GetBoolean());
        Assert.Equal(90, body.GetProperty("retentionDays").GetInt32());
        Assert.Contains(body.GetProperty("sources").EnumerateArray(), source => source.GetString() == "vault");

        var events = await session.GetWithCookiesAsync($"{Root}/events?sources=vault,console&limit=20");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, events.StatusCode);
        Assert.Equal("infra_audit_unavailable",
            (await events.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());

        var mine = await session.GetWithCookiesAsync($"{IdentityApiRoutes.Admin}/infra-audit/me/events");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, mine.StatusCode);
    }

    [Fact]
    public async Task Invalid_queries_are_rejected_before_reaching_loki()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        foreach (var query in new[] { "sources=unknown", "result=maybe", "limit=0", "limit=100000",
                     $"from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-200).ToString("O"))}" })
        {
            var response = await session.GetWithCookiesAsync($"{Root}/events?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_infra_audit_query",
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());
        }
    }

    [Fact]
    public async Task Export_requires_a_fresh_mfa()
    {
        using var session = fixture.CreateSessionClient();
        var login = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // Password-only cookie session: reading is allowed, exporting is not.
        var meta = await session.GetWithCookiesAsync($"{Root}/meta");
        Assert.Equal(HttpStatusCode.OK, meta.StatusCode);
        var export = await session.GetWithCookiesAsync($"{Root}/export");

        Assert.Equal(HttpStatusCode.Forbidden, export.StatusCode);
        Assert.Equal("step_up_required",
            (await export.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());
    }

    [Fact]
    public void Csv_export_neutralizes_formulas_and_quotes_cells()
    {
        var csv = InfraAuditEndpoints.ToCsv([
            new InfraAuditEvent(new DateTimeOffset(2026, 11, 25, 1, 2, 3, TimeSpan.Zero), "console", "=HYPERLINK(\"x\")",
                "GET vault", "vault:/ui", InfraAuditResults.Allowed, null, null, "a,b", "console_audit"),
        ]);

        var row = csv.Split("\r\n")[1];
        Assert.StartsWith("2026-11-25T01:02:03.0000000Z,console,", row);
        Assert.Contains("\"'=HYPERLINK(\"\"x\"\")\"", row);
        Assert.Contains("\"a,b\"", row);
    }
}
