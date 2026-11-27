using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.HostSecurity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class HostIntegrityApprovalEndpointTests(IdentityServiceTestFixture fixture)
{
    private static readonly string Root = $"{IdentityApiRoutes.AdminIam}/host-security";

    [Fact]
    public async Task Approve_requires_fresh_mfa_and_valid_input()
    {
        using var passwordOnly = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await passwordOnly.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var stepUp = await passwordOnly.PostWithCookiesAsync($"{Root}/approvals",
            new { host = "vmi3129107", fingerprint = "3dec450c11effb93", reason = "planned sudoers change" });
        Assert.Equal(HttpStatusCode.Forbidden, stepUp.StatusCode);

        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var bad = await session.PostWithCookiesAsync($"{Root}/approvals", new { host = "vmi3129107", fingerprint = "nothex", reason = "planned sudoers change" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("invalid_fingerprint", (await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reason").GetString());

        // Valid input, but the drift is looked up in Loki first (not configured here).
        var unavailable = await session.PostWithCookiesAsync($"{Root}/approvals",
            new { host = "vmi3129107", fingerprint = "3dec450c11effb93", reason = "planned sudoers change" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);

        var list = await session.GetWithCookiesAsync($"{Root}/approvals");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public async Task Hosts_pull_only_their_own_approvals_with_the_workload_scope()
    {
        var host = $"test-{Guid.NewGuid():N}"[..20];
        await using (var seed = fixture.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<IdentityDbContext>();
            foreach (var (h, fp) in new[] { (host, "0123456789abcdef"), ("other-host", "fedcba9876543210") })
                db.AuditLogs.Add(new AuditLog
                {
                    UserId = Guid.NewGuid().ToString("D"), UserName = "bob@example.com",
                    Action = HostIntegrityApprovalStore.Approved, ResourceType = HostIntegrityApprovalStore.ResourceType,
                    ResourceId = $"drift:{h}:{fp}",
                    Details = JsonSerializer.Serialize(new { Reason = "planned change", Severity = "warning", Host = h, Fingerprint = fp, ChangeCount = 1 }),
                    Outcome = "success", Source = "host-security",
                });
            await db.SaveChangesAsync();
        }
        var url = $"{IdentityApiRoutes.OpenShipHostIntegrityApprovals}?host={host}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.AnonymousClient.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PullAsync(url, await fixture.CreateFreshMfaAdminTokenAsync())).StatusCode);
        var wrongScope = await CreateWorkloadTokenAsync(IdentityApiRoutes.OpenShipConsoleAccessScope);
        Assert.Equal(HttpStatusCode.Forbidden, (await PullAsync(url, wrongScope)).StatusCode);

        var token = await CreateWorkloadTokenAsync(IdentityApiRoutes.OpenShipHostIntegrityApprovalsScope);
        var pulled = await PullAsync(url, token);
        var body = await pulled.Content.ReadAsStringAsync();
        Assert.True(pulled.StatusCode == HttpStatusCode.OK, body);
        var items = JsonDocument.Parse(body).RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("0123456789abcdef", items[0].GetProperty("fingerprint").GetString());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PullAsync($"{IdentityApiRoutes.OpenShipHostIntegrityApprovals}?host=bad%20host", token)).StatusCode);
    }

    private async Task<HttpResponseMessage> PullAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await fixture.AnonymousClient.SendAsync(request);
    }

    private async Task<string> CreateWorkloadTokenAsync(string scopeName)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var clientId = $"openship-host-integrity-{suffix}";
        var clientSecret = $"secret-{Guid.NewGuid():N}{Guid.NewGuid():N}";
        await using (var seed = fixture.Services.CreateAsyncScope())
        {
            var audience = seed.ServiceProvider.GetRequiredService<IConfiguration>()["Jwt:Audience"]!;
            var db = seed.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var scope = new IamScope { Key = $"tenant-host-integrity-{suffix}", DisplayName = "Host integrity tenant", Kind = "tenant" };
            db.IamScopes.Add(scope);
            db.IamWorkloadRoles.Add(new IamWorkloadRole
            {
                Key = clientId, DisplayName = "Host integrity approvals", ScopeId = scope.Id, Audience = audience,
                TrustPolicyJson = JsonSerializer.Serialize(new { principals = new[] { clientId } }),
                PermissionsJson = "[]", MaxSessionSeconds = 900
            });
            await db.SaveChangesAsync();
            var scopes = seed.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
            if (await scopes.FindByNameAsync(scopeName) is { } existing)
            {
                var d = new OpenIddictScopeDescriptor();
                await scopes.PopulateAsync(d, existing);
                if (d.Resources.Add(audience)) await scopes.UpdateAsync(existing, d);
            }
            else
            {
                var d = new OpenIddictScopeDescriptor { Name = scopeName };
                d.Resources.Add(audience);
                await scopes.CreateAsync(d);
            }
            var applications = seed.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId, ClientSecret = clientSecret,
                ClientType = OpenIddictConstants.ClientTypes.Confidential, DisplayName = "Host integrity approvals"
            };
            descriptor.Permissions.UnionWith(new[]
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + scopeName
            });
            await applications.CreateAsync(descriptor);
        }
        var tokenResponse = await fixture.AnonymousClient.PostAsync(IdentityApiRoutes.OidcToken, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = OpenIddictConstants.GrantTypes.ClientCredentials,
            ["client_id"] = clientId, ["client_secret"] = clientSecret, ["scope"] = scopeName
        }));
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.IsSuccessStatusCode, tokenBody);
        return JsonDocument.Parse(tokenBody).RootElement.GetProperty("access_token").GetString()!;
    }
}
