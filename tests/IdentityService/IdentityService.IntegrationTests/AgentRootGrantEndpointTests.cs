using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

/// <summary>
/// Four-eyes agent root SSH grants: request, approval by another super admin (signed by a
/// fake Vault SSH CA), certificate distribution to the workload fetcher and revocation.
/// </summary>
[Collection("IdentityServiceIntegration")]
public sealed class AgentRootGrantEndpointTests(IdentityServiceTestFixture fixture)
{
    private static readonly string Root = $"{IdentityApiRoutes.AdminIam}/infra-access/agent-root-grants";
    private const string AgentId = "openship-agent-root";

    [Fact]
    public async Task Anonymous_callers_cannot_reach_agent_root_endpoints()
    {
        Assert.True((await fixture.AnonymousClient.GetAsync(Root)).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.AnonymousClient.GetAsync($"{IdentityApiRoutes.OpenShipAgentRootCertificate}?agentId={AgentId}")).StatusCode);
    }

    [Fact]
    public async Task Requester_cannot_approve_their_own_grant()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var created = await session.PostWithCookiesAsync(Root, new { durationMinutes = 60, reason = "Patch kubelet on all nodes" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var grant = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", grant.GetProperty("status").GetString());
        Assert.True(grant.GetProperty("requestedByMe").GetBoolean());
        var id = grant.GetProperty("id").GetGuid();

        var approve = await session.PostWithCookiesAsync($"{Root}/{id}/approve");
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
        Assert.Equal("agent_root_self_approval", await ErrorCodeAsync(approve));
        Assert.Empty(fixture.AgentRootSigner.Calls.Where(call => call.KeyId.Contains(id.ToString("D"), StringComparison.Ordinal)));

        await using var scope = fixture.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AgentRootGrants.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(AgentRootGrant.StatusPending, stored.Status);
        Assert.Equal(IdentityTestData.AdminId, stored.RequestedBy);
        Assert.Equal(IdentityTestData.AdminEmail, stored.RequestedByEmail, ignoreCase: true);
    }

    [Fact]
    public async Task Create_validates_duration_and_reason_and_requires_fresh_mfa()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var tooShort = await session.PostWithCookiesAsync(Root, new { durationMinutes = 10, reason = "Patch kubelet on all nodes" });
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        var noReason = await session.PostWithCookiesAsync(Root, new { durationMinutes = 60, reason = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        using var passwordOnly = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await passwordOnly.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var stepUp = await passwordOnly.PostWithCookiesAsync(Root, new { durationMinutes = 60, reason = "Patch kubelet on all nodes" });
        Assert.Equal(HttpStatusCode.Forbidden, stepUp.StatusCode);
        Assert.Equal("step_up_required", await ErrorCodeAsync(stepUp));
    }

    [Fact]
    public async Task Another_super_admin_approves_then_the_workload_fetches_the_certificate_until_revoked()
    {
        await RevokeAllActiveAsync();
        var otherRequester = Guid.NewGuid();
        var id = await SeedPendingAsync(otherRequester, "requester-two@example.test", durationMinutes: 120);
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var approve = await session.PostWithCookiesAsync($"{Root}/{id}/approve");
        var approveBody = await approve.Content.ReadAsStringAsync();
        Assert.True(approve.StatusCode == HttpStatusCode.OK, approveBody);
        var approved = JsonDocument.Parse(approveBody).RootElement;
        Assert.Equal("issued", approved.GetProperty("status").GetString());
        Assert.True(approved.GetProperty("active").GetBoolean());
        Assert.InRange(approved.GetProperty("remainingMinutes").GetInt32(), 119, 120);
        Assert.False(approved.TryGetProperty("certificate", out _));

        var call = Assert.Single(fixture.AgentRootSigner.Calls, item => item.KeyId.Contains(id.ToString("D"), StringComparison.Ordinal));
        Assert.Equal(FakeAgentRootCertificateSigner.PublicKey, call.PublicKey);
        Assert.Equal("root-breakglass", call.Principal);
        Assert.Equal(TimeSpan.FromMinutes(120), call.Ttl);
        Assert.Equal($"agent-root grant={id:D} requester=requester-two@example.test approver={IdentityTestData.AdminEmail}",
            call.KeyId, ignoreCase: true);

        // The list never carries the certificate text.
        var list = await session.GetWithCookiesAsync(Root);
        var listBody = await list.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.DoesNotContain("ssh-ed25519-cert-v01", listBody, StringComparison.Ordinal);
        var row = JsonDocument.Parse(listBody).RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == id);
        Assert.False(row.TryGetProperty("certificate", out _));
        Assert.False(row.GetProperty("requestedByMe").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("certificateSerial").GetString()));

        // Workload without the dedicated scope is refused; with it, the certificate is returned.
        var wrongScope = await CreateWorkloadTokenAsync(IdentityApiRoutes.OpenShipConsoleAccessScope);
        Assert.Equal(HttpStatusCode.Forbidden, (await FetchAsync(wrongScope)).StatusCode);
        // A human super-admin token never qualifies either.
        Assert.Equal(HttpStatusCode.Forbidden, (await FetchAsync(await fixture.CreateFreshMfaAdminTokenAsync())).StatusCode);

        var token = await CreateWorkloadTokenAsync(IdentityApiRoutes.OpenShipAgentRootCertificateScope);
        var fetched = await FetchAsync(token);
        var fetchedBody = await fetched.Content.ReadAsStringAsync();
        Assert.True(fetched.StatusCode == HttpStatusCode.OK, fetchedBody);
        Assert.True(fetched.Headers.CacheControl?.NoStore, "certificates must not be cached");
        var certificate = JsonDocument.Parse(fetchedBody).RootElement;
        Assert.Equal(id, certificate.GetProperty("grantId").GetGuid());
        Assert.StartsWith("ssh-ed25519-cert-v01@openssh.com ", certificate.GetProperty("certificate").GetString());
        Assert.Equal(row.GetProperty("certificateSerial").GetString(), certificate.GetProperty("serial").GetString());

        var revoke = await session.PostWithCookiesAsync($"{Root}/{id}/revoke");
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal("revoked", (await revoke.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await FetchAsync(token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await session.PostWithCookiesAsync($"{Root}/{id}/revoke")).StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var audit = await db.AuditLogs.AsNoTracking()
            .Where(item => item.ResourceType == "AgentRootGrant" && item.ResourceId == id.ToString("D"))
            .ToListAsync();
        Assert.Contains(audit, item => item.Action == "AUTHZ_AGENT_ROOT_GRANT_APPROVE");
        Assert.Contains(audit, item => item.Action == "AUTHZ_AGENT_ROOT_GRANT_REVOKE");
        Assert.DoesNotContain(audit, item => (item.AfterJson ?? string.Empty).Contains("ssh-ed25519-cert-v01", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Vault_failure_marks_the_grant_failed_and_answers_502()
    {
        var id = await SeedPendingAsync(Guid.NewGuid(), "requester-three@example.test", durationMinutes: 30);
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        fixture.AgentRootSigner.FailWith = "Vault refused to sign the agent key (HTTP 403).";
        try
        {
            var approve = await session.PostWithCookiesAsync($"{Root}/{id}/approve");
            Assert.Equal(HttpStatusCode.BadGateway, approve.StatusCode);
            Assert.Equal("agent_root_sign_failed", await ErrorCodeAsync(approve));
        }
        finally
        {
            fixture.AgentRootSigner.FailWith = null;
        }

        await using var scope = fixture.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AgentRootGrants.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(AgentRootGrant.StatusFailed, stored.Status);
        Assert.Null(stored.Certificate);
        Assert.Contains("HTTP 403", stored.FailureReason);
    }

    [Fact]
    public async Task Pending_grant_past_the_approval_window_expires_instead_of_being_approved()
    {
        var id = await SeedPendingAsync(Guid.NewGuid(), "requester-four@example.test", durationMinutes: 60,
            requestedAt: DateTime.UtcNow.AddMinutes(-61));
        using var session = await fixture.CreateAuthenticatedSessionAsync();

        var approve = await session.PostWithCookiesAsync($"{Root}/{id}/approve");
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AgentRootGrants.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(AgentRootGrant.StatusExpired, stored.Status);
        Assert.Empty(fixture.AgentRootSigner.Calls.Where(call => call.KeyId.Contains(id.ToString("D"), StringComparison.Ordinal)));
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("errorCode", out var code) ? code.GetString() : null;

    private async Task<Guid> SeedPendingAsync(Guid requester, string requesterEmail, int durationMinutes, DateTime? requestedAt = null)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var grant = new AgentRootGrant
        {
            Id = Guid.NewGuid(), AgentId = AgentId, Reason = "Rotate the etcd encryption key", DurationMinutes = durationMinutes,
            Status = AgentRootGrant.StatusPending, RequestedBy = requester, RequestedByEmail = requesterEmail,
            RequestedAt = requestedAt ?? DateTime.UtcNow,
        };
        db.AgentRootGrants.Add(grant);
        await db.SaveChangesAsync();
        return grant.Id;
    }

    private async Task RevokeAllActiveAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.AgentRootGrants.Where(item => item.Status == AgentRootGrant.StatusIssued)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, AgentRootGrant.StatusRevoked)
                .SetProperty(item => item.RevokedAt, DateTime.UtcNow));
    }

    private async Task<HttpResponseMessage> FetchAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{IdentityApiRoutes.OpenShipAgentRootCertificate}?agentId={AgentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await fixture.AnonymousClient.SendAsync(request);
    }

    private async Task<string> CreateWorkloadTokenAsync(string scopeName)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var clientId = $"openship-agent-root-fetcher-{suffix}";
        var clientSecret = $"secret-{Guid.NewGuid():N}{Guid.NewGuid():N}";

        await using (var seed = fixture.Services.CreateAsyncScope())
        {
            var audience = seed.ServiceProvider.GetRequiredService<IConfiguration>()["Jwt:Audience"];
            Assert.False(string.IsNullOrWhiteSpace(audience));
            var db = seed.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var scope = new IamScope { Key = $"tenant-agent-root-{suffix}", DisplayName = "Agent root fetcher tenant", Kind = "tenant" };
            db.IamScopes.Add(scope);
            db.IamWorkloadRoles.Add(new IamWorkloadRole
            {
                Key = clientId,
                DisplayName = "Agent root certificate fetcher",
                ScopeId = scope.Id,
                Audience = audience!,
                TrustPolicyJson = JsonSerializer.Serialize(new { principals = new[] { clientId } }),
                PermissionsJson = "[]",
                MaxSessionSeconds = 900
            });
            await db.SaveChangesAsync();

            var scopes = seed.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
            // The AddAgentRootGrants migration already registers the agent scope (for the
            // production audience); make sure the test audience is one of its resources.
            if (await scopes.FindByNameAsync(scopeName) is { } existingScope)
            {
                var scopeDescriptor = new OpenIddictScopeDescriptor();
                await scopes.PopulateAsync(scopeDescriptor, existingScope);
                if (scopeDescriptor.Resources.Add(audience!))
                    await scopes.UpdateAsync(existingScope, scopeDescriptor);
            }
            else
            {
                var scopeDescriptor = new OpenIddictScopeDescriptor { Name = scopeName };
                scopeDescriptor.Resources.Add(audience!);
                await scopes.CreateAsync(scopeDescriptor);
            }

            var applications = seed.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                DisplayName = "Agent root certificate fetcher"
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
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = scopeName
        }));
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.IsSuccessStatusCode, tokenBody);
        return JsonDocument.Parse(tokenBody).RootElement.GetProperty("access_token").GetString()!;
    }
}
