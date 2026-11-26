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
    private static readonly string Devices = $"{IdentityApiRoutes.AdminIam}/infra-access/agent-root-devices";
    private const string AgentId = "openship-agent-root";

    /// <summary>The K3s manager device seeded by migration 20261126110000.</summary>
    private static readonly Guid ManagerDeviceId = Guid.Parse("7d0c5c2e-5b7a-4f37-9d3c-0a1f6e2b9c41");

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

        var created = await session.PostWithCookiesAsync(Root,
            new { durationMinutes = 60, reason = "Patch kubelet on all nodes", deviceIds = new[] { ManagerDeviceId } });
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
        var noReason = await session.PostWithCookiesAsync(Root, new { durationMinutes = 60, reason = "short", deviceIds = new[] { ManagerDeviceId } });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        var noDevice = await session.PostWithCookiesAsync(Root, new { durationMinutes = 60, reason = "Patch kubelet on all nodes" });
        Assert.Equal(HttpStatusCode.BadRequest, noDevice.StatusCode);
        var unknownDevice = await session.PostWithCookiesAsync(Root,
            new { durationMinutes = 60, reason = "Patch kubelet on all nodes", deviceIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.BadRequest, unknownDevice.StatusCode);
        var tooLong = await session.PostWithCookiesAsync(Root,
            new { durationMinutes = 1441, reason = "Patch kubelet on all nodes", deviceIds = new[] { ManagerDeviceId } });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var fullDay = await session.PostWithCookiesAsync(Root,
            new { durationMinutes = 1440, reason = "Overnight etcd defragmentation", deviceIds = new[] { ManagerDeviceId } });
        Assert.Equal(HttpStatusCode.Created, fullDay.StatusCode);

        using var passwordOnly = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await passwordOnly.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var stepUp = await passwordOnly.PostWithCookiesAsync(Root, new { durationMinutes = 60, reason = "Patch kubelet on all nodes" });
        Assert.Equal(HttpStatusCode.Forbidden, stepUp.StatusCode);
        Assert.Equal("step_up_required", await ErrorCodeAsync(stepUp));
    }

    [Fact]
    public async Task Approval_signs_every_active_device_and_each_device_fetches_only_its_own_certificate()
    {
        await RevokeAllActiveAsync();
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var (clientA, tokenA) = await CreateWorkloadClientAsync(IdentityApiRoutes.OpenShipAgentRootCertificateScope);
        var (clientB, tokenB) = await CreateWorkloadClientAsync(IdentityApiRoutes.OpenShipAgentRootCertificateScope);
        var keyA = NewPublicKey("laptop-a");
        var keyB = NewPublicKey("laptop-b");
        var nameA = $"laptop-a-{Guid.NewGuid():N}"[..20];
        var nameB = $"laptop-b-{Guid.NewGuid():N}"[..20];
        var deviceA = await AddDeviceAsync(session, nameA, keyA, clientA);
        await AddDeviceAsync(session, nameB, keyB, clientB);

        // A registered client with no grant yet gets 404; an unregistered client too.
        Assert.Equal(HttpStatusCode.NotFound, (await FetchAsync(tokenA)).StatusCode);
        var (_, strangerToken) = await CreateWorkloadClientAsync(IdentityApiRoutes.OpenShipAgentRootCertificateScope);
        var stranger = await FetchAsync(strangerToken);
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Equal("agent_root_unknown_device", await ErrorCodeAsync(stranger));

        var id = await SeedPendingAsync(Guid.NewGuid(), "requester-two@example.test", durationMinutes: 120);
        var approve = await session.PostWithCookiesAsync($"{Root}/{id}/approve");
        var approveBody = await approve.Content.ReadAsStringAsync();
        Assert.True(approve.StatusCode == HttpStatusCode.OK, approveBody);
        var approved = JsonDocument.Parse(approveBody).RootElement;
        Assert.Equal("issued", approved.GetProperty("status").GetString());
        Assert.InRange(approved.GetProperty("remainingMinutes").GetInt32(), 119, 120);
        Assert.False(approved.TryGetProperty("certificate", out _));
        var issuedNames = approved.GetProperty("certificates").EnumerateArray()
            .Select(item => item.GetProperty("deviceName").GetString()).ToList();
        Assert.Contains(nameA, issuedNames);
        Assert.Contains(nameB, issuedNames);

        var callA = Assert.Single(fixture.AgentRootSigner.Calls, item => item.PublicKey == keyA);
        Assert.Equal("root-breakglass", callA.Principal);
        Assert.Equal(TimeSpan.FromMinutes(120), callA.Ttl);
        Assert.Equal($"agent-root grant={id:D} device={nameA} requester=requester-two@example.test approver={IdentityTestData.AdminEmail}",
            callA.KeyId, ignoreCase: true);
        Assert.Single(fixture.AgentRootSigner.Calls, item => item.PublicKey == keyB && item.KeyId.Contains(id.ToString("D"), StringComparison.Ordinal));

        // The list never carries certificate text, only device names and serials.
        var list = await session.GetWithCookiesAsync(Root);
        var listBody = await list.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.DoesNotContain("ssh-ed25519-cert-v01", listBody, StringComparison.Ordinal);
        var listRoot = JsonDocument.Parse(listBody).RootElement;
        Assert.Contains(listRoot.GetProperty("activeDevices").EnumerateArray(), item => item.GetProperty("name").GetString() == nameA);
        var row = listRoot.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id);
        var serialA = row.GetProperty("certificates").EnumerateArray()
            .Single(item => item.GetProperty("deviceName").GetString() == nameA).GetProperty("serial").GetString();

        // Without the dedicated scope, or as a human, the fetch is refused.
        var (_, wrongScope) = await CreateWorkloadClientAsync(IdentityApiRoutes.OpenShipConsoleAccessScope);
        Assert.Equal(HttpStatusCode.Forbidden, (await FetchAsync(wrongScope)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await FetchAsync(await fixture.CreateFreshMfaAdminTokenAsync())).StatusCode);

        var fetched = await FetchAsync(tokenA);
        var fetchedBody = await fetched.Content.ReadAsStringAsync();
        Assert.True(fetched.StatusCode == HttpStatusCode.OK, fetchedBody);
        Assert.True(fetched.Headers.CacheControl?.NoStore, "certificates must not be cached");
        var certificate = JsonDocument.Parse(fetchedBody).RootElement;
        Assert.Equal(id, certificate.GetProperty("grantId").GetGuid());
        Assert.Equal(nameA, certificate.GetProperty("deviceName").GetString());
        Assert.Equal(serialA, certificate.GetProperty("serial").GetString());
        Assert.StartsWith("ssh-ed25519-cert-v01@openssh.com ", certificate.GetProperty("certificate").GetString());
        var fetchedB = JsonDocument.Parse(await (await FetchAsync(tokenB)).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(nameB, fetchedB.GetProperty("deviceName").GetString());
        Assert.NotEqual(serialA, fetchedB.GetProperty("serial").GetString());

        // Disabling a device stops its fetches at once; the other device keeps working.
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{Devices}/{deviceA}/disable")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await FetchAsync(tokenA)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await FetchAsync(tokenB)).StatusCode);

        var revoke = await session.PostWithCookiesAsync($"{Root}/{id}/revoke");
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal("revoked", (await revoke.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await FetchAsync(tokenB)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await session.PostWithCookiesAsync($"{Root}/{id}/revoke")).StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var audit = await db.AuditLogs.AsNoTracking()
            .Where(item => (item.ResourceType == "AgentRootGrant" && item.ResourceId == id.ToString("D")) ||
                           (item.ResourceType == "AgentRootDevice" && item.ResourceId == deviceA.ToString("D")))
            .ToListAsync();
        Assert.Contains(audit, item => item.Action == "AUTHZ_AGENT_ROOT_GRANT_APPROVE");
        Assert.Contains(audit, item => item.Action == "AUTHZ_AGENT_ROOT_GRANT_REVOKE");
        Assert.Contains(audit, item => item.Action == "AUTHZ_AGENT_ROOT_DEVICE_ADD");
        Assert.Contains(audit, item => item.Action == "AUTHZ_AGENT_ROOT_DEVICE_DISABLE");
        Assert.DoesNotContain(audit, item => (item.AfterJson ?? string.Empty).Contains("ssh-ed25519-cert-v01", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Approval_signs_only_the_devices_chosen_in_the_request()
    {
        await RevokeAllActiveAsync();
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var (clientA, tokenA) = await CreateWorkloadClientAsync(IdentityApiRoutes.OpenShipAgentRootCertificateScope);
        var (clientB, tokenB) = await CreateWorkloadClientAsync(IdentityApiRoutes.OpenShipAgentRootCertificateScope);
        var (clientC, tokenC) = await CreateWorkloadClientAsync(IdentityApiRoutes.OpenShipAgentRootCertificateScope);
        var keyA = NewPublicKey("chosen-a");
        var keyB = NewPublicKey("chosen-b");
        var keyC = NewPublicKey("not-chosen");
        var nameA = $"pick-a-{Guid.NewGuid():N}"[..18];
        var nameB = $"pick-b-{Guid.NewGuid():N}"[..18];
        var nameC = $"pick-c-{Guid.NewGuid():N}"[..18];
        var deviceA = await AddDeviceAsync(session, nameA, keyA, clientA);
        var deviceB = await AddDeviceAsync(session, nameB, keyB, clientB);
        await AddDeviceAsync(session, nameC, keyC, clientC);

        var id = await SeedPendingAsync(Guid.NewGuid(), "requester-six@example.test", durationMinutes: 1440,
            deviceIds: [deviceA, deviceB]);
        var list = JsonDocument.Parse(await (await session.GetWithCookiesAsync(Root)).Content.ReadAsStringAsync()).RootElement;
        var pending = list.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id);
        Assert.Equal(new[] { nameA, nameB }.Order(StringComparer.Ordinal),
            pending.GetProperty("requestedDevices").EnumerateArray().Select(item => item.GetString()!));
        Assert.Contains(list.GetProperty("activeDevices").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == deviceA && item.GetProperty("name").GetString() == nameA);

        var approve = await session.PostWithCookiesAsync($"{Root}/{id}/approve");
        Assert.True(approve.StatusCode == HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        var calls = fixture.AgentRootSigner.Calls.Where(call => call.KeyId.Contains(id.ToString("D"), StringComparison.Ordinal)).ToList();
        Assert.Equal(2, calls.Count);
        Assert.Contains(calls, call => call.PublicKey == keyA && call.Ttl == TimeSpan.FromHours(24));
        Assert.Contains(calls, call => call.PublicKey == keyB);
        Assert.DoesNotContain(calls, call => call.PublicKey == keyC);

        Assert.Equal(HttpStatusCode.OK, (await FetchAsync(tokenA)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await FetchAsync(tokenB)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await FetchAsync(tokenC)).StatusCode);
        await RevokeAllActiveAsync();
    }

    [Fact]
    public async Task Adding_a_device_validates_key_client_and_uniqueness()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var (client, _) = await CreateWorkloadClientAsync(IdentityApiRoutes.OpenShipAgentRootCertificateScope);
        var key = NewPublicKey("validation");
        var name = $"dev-{Guid.NewGuid():N}"[..16];

        Assert.Equal(HttpStatusCode.BadRequest, (await session.PostWithCookiesAsync(Devices,
            new { name, publicKey = "ssh-rsa AAAAB3NzaC1yc2E bad", clientId = client })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await session.PostWithCookiesAsync(Devices,
            new { name = "bad name!", publicKey = key, clientId = client })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await session.PostWithCookiesAsync(Devices,
            new { name, publicKey = key, clientId = "no-such-client-" + Guid.NewGuid().ToString("N") })).StatusCode);

        await AddDeviceAsync(session, name, key, client);
        var sameClient = await session.PostWithCookiesAsync(Devices, new { name = name + "-2", publicKey = NewPublicKey("other"), clientId = client });
        Assert.Equal(HttpStatusCode.Conflict, sameClient.StatusCode);
        Assert.Equal("agent_root_client_in_use", await ErrorCodeAsync(sameClient));
        var sameKey = await session.PostWithCookiesAsync(Devices, new { name = name + "-3", publicKey = key, clientId = client });
        Assert.Equal(HttpStatusCode.Conflict, sameKey.StatusCode);

        var listed = JsonDocument.Parse(await (await session.GetWithCookiesAsync(Devices)).Content.ReadAsStringAsync()).RootElement;
        var row = listed.EnumerateArray().Single(item => item.GetProperty("name").GetString() == name);
        Assert.StartsWith("SHA256:", row.GetProperty("fingerprint").GetString());
        Assert.False(row.TryGetProperty("publicKey", out _));

        using var passwordOnly = fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await passwordOnly.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var stepUp = await passwordOnly.PostWithCookiesAsync(Devices, new { name = name + "-4", publicKey = NewPublicKey("mfa"), clientId = client });
        Assert.Equal(HttpStatusCode.Forbidden, stepUp.StatusCode);
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
        Assert.False(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AgentRootCertificates.AnyAsync(item => item.GrantId == id));
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

    private async Task<Guid> SeedPendingAsync(Guid requester, string requesterEmail, int durationMinutes, DateTime? requestedAt = null,
        Guid[]? deviceIds = null)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var grant = new AgentRootGrant
        {
            Id = Guid.NewGuid(), AgentId = AgentId, Reason = "Rotate the etcd encryption key", DurationMinutes = durationMinutes,
            Status = AgentRootGrant.StatusPending, RequestedBy = requester, RequestedByEmail = requesterEmail,
            RequestedAt = requestedAt ?? DateTime.UtcNow,
            RequestedDeviceIds = deviceIds is null ? null : JsonSerializer.Serialize(deviceIds),
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

    /// <summary>A fresh, valid "ssh-ed25519 AAAA... comment" line (random 32-byte key).</summary>
    private static string NewPublicKey(string comment)
    {
        var type = System.Text.Encoding.ASCII.GetBytes("ssh-ed25519");
        var blob = new byte[4 + type.Length + 4 + 32];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(blob, type.Length);
        type.CopyTo(blob, 4);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(blob.AsSpan(4 + type.Length), 32);
        System.Security.Cryptography.RandomNumberGenerator.Fill(blob.AsSpan(8 + type.Length));
        return $"ssh-ed25519 {Convert.ToBase64String(blob)} {comment}@test";
    }

    private static async Task<Guid> AddDeviceAsync(SessionClient session, string name, string publicKey, string clientId)
    {
        var response = await session.PostWithCookiesAsync(Devices, new { name, publicKey, clientId });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> FetchAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{IdentityApiRoutes.OpenShipAgentRootCertificate}?agentId={AgentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await fixture.AnonymousClient.SendAsync(request);
    }

    private async Task<(string ClientId, string Token)> CreateWorkloadClientAsync(string scopeName)
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
        return (clientId, JsonDocument.Parse(tokenBody).RootElement.GetProperty("access_token").GetString()!);
    }
}
