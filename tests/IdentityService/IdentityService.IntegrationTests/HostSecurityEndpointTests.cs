using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.HostSecurity;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class HostSecurityEndpointTests(IdentityServiceTestFixture fixture)
{
    private static readonly string Root = $"{IdentityApiRoutes.AdminIam}/host-security";

    [Fact]
    public async Task Anonymous_callers_cannot_read_or_acknowledge()
    {
        foreach (var route in new[] { $"{Root}/meta", $"{Root}/events", $"{Root}/hosts" })
        {
            var response = await fixture.AnonymousClient.GetAsync(route);
            Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect, route);
        }
        var ack = await fixture.AnonymousClient.PostAsJsonAsync($"{Root}/acks", new { items = new[] { new { key = "evt:0123456789abcdef0123" } } });
        Assert.True(ack.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unconfigured_host_reports_meta_and_answers_503()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var meta = await session.GetWithCookiesAsync($"{Root}/meta");
        Assert.Equal(HttpStatusCode.OK, meta.StatusCode);
        var body = await meta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("configured").GetBoolean());
        Assert.Contains(body.GetProperty("categories").EnumerateArray(), item => item.GetString() == "runtime");

        foreach (var route in new[] { $"{Root}/events?minSeverity=warning", $"{Root}/hosts" })
        {
            var response = await session.GetWithCookiesAsync(route);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("host_security_unavailable",
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());
        }
    }

    [Fact]
    public async Task Invalid_queries_are_rejected_before_reaching_loki()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        foreach (var query in new[] { "categories=unknown", "minSeverity=loud", "host=bad%20host", "limit=0", "limit=100000",
                     $"from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-200).ToString("O"))}" })
        {
            var response = await session.GetWithCookiesAsync($"{Root}/events?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_host_security_request",
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());
        }
    }

    [Fact]
    public async Task Acknowledgement_is_stored_once_in_the_audit_log()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var key = $"drift:vmi-test-{Guid.NewGuid():N}"[..30] + ":0123456789abcdef";

        var bad = await session.PostWithCookiesAsync($"{Root}/acks", new { items = new[] { new { key = "drift:x; DROP" } } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var request = new { items = new[] { new { key, severity = "critical", host = "vmi-test", securityEvent = "integrity_drift" } }, note = "planned change" };
        var first = await session.PostWithCookiesAsync($"{Root}/acks", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(1, (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("acknowledged").GetInt32());

        var second = await session.PostWithCookiesAsync($"{Root}/acks", request);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("acknowledged").GetInt32());
        Assert.Equal("planned change", body.GetProperty("items")[0].GetProperty("note").GetString());

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(1, await db.AuditLogs.CountAsync(row =>
            row.ResourceType == AuditLogHostSecurityAckStore.ResourceType && row.ResourceId == key));
    }
}
