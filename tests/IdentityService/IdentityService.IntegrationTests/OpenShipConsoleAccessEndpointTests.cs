using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

/// <summary>
/// IdentityService as the source of truth for OpenShip console access: the
/// workload lookup API, leaver revocation and IAM group member management.
/// </summary>
[Collection("IdentityServiceIntegration")]
public sealed class OpenShipConsoleAccessEndpointTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Console_access_returns_prefixed_active_groups_and_break_glass_write_window()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var email = $"Console-Ops-{suffix}@Example.test";
        var expiresAt = DateTime.UtcNow.AddMinutes(20);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userId = await CreateUserAsync(scope.ServiceProvider, email);
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var iamScope = new IamScope { Key = $"tenant-console-{suffix}", DisplayName = "Console tenant", Kind = "tenant" };
            db.IamScopes.Add(iamScope);
            var read = new IamGroup { Key = $"openship-console-read-{suffix}", DisplayName = "Read", ScopeId = iamScope.Id };
            var retired = new IamGroup { Key = $"openship-console-retired-{suffix}", DisplayName = "Retired", ScopeId = iamScope.Id, IsActive = false };
            var other = new IamGroup { Key = $"finance-{suffix}", DisplayName = "Finance", ScopeId = iamScope.Id };
            db.IamGroups.AddRange(read, retired, other);
            db.IamGroupMemberships.AddRange(
                new IamGroupMembership { GroupId = read.Id, UserId = userId },
                new IamGroupMembership { GroupId = retired.Id, UserId = userId },
                new IamGroupMembership { GroupId = other.Id, UserId = userId });
            db.BreakGlassRequests.Add(new BreakGlassRequest
            {
                Id = Guid.NewGuid(), SubjectUserId = userId, PermissionCode = "openship.console.write",
                FacilityId = "facility-a", Reason = "Console write incident", Status = "approved",
                RequestedBy = Guid.NewGuid().ToString(), ApprovedBy = Guid.NewGuid().ToString(),
                RequestedAt = DateTime.UtcNow, ApprovedAt = DateTime.UtcNow, ExpiresAt = expiresAt
            });
            await db.SaveChangesAsync();
        }

        var token = await CreateWorkloadTokenAsync(IdentityApiRoutes.OpenShipConsoleAccessScope);
        var response = await LookupAsync(token, email.ToUpperInvariant());
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore, "console-access responses must not be cached");
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(email.ToLowerInvariant(), json.GetProperty("email").GetString());
        Assert.True(json.GetProperty("active").GetBoolean());
        Assert.Equal([$"openship-console-read-{suffix}"], json.GetProperty("groups").EnumerateArray().Select(item => item.GetString()));
        var writeUntil = DateTimeOffset.Parse(json.GetProperty("writeUntil").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(Math.Abs((writeUntil.UtcDateTime - expiresAt).TotalSeconds) < 1);
        // Only the console decision is returned: no ids, names or other PII.
        Assert.Equal(["email", "active", "groups", "writeUntil"], json.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task Console_access_returns_404_for_unknown_and_inactive_users()
    {
        var inactiveEmail = $"console-leaver-{Guid.NewGuid():N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
            await CreateUserAsync(scope.ServiceProvider, inactiveEmail, isActive: false);

        var token = await CreateWorkloadTokenAsync(IdentityApiRoutes.OpenShipConsoleAccessScope);

        Assert.Equal(HttpStatusCode.NotFound, (await LookupAsync(token, $"nobody-{Guid.NewGuid():N}@example.test")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await LookupAsync(token, inactiveEmail)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await LookupAsync(token, "")).StatusCode);
    }

    [Fact]
    public async Task Console_access_requires_a_workload_token_with_the_console_access_scope()
    {
        var wrongScopeToken = await CreateWorkloadTokenAsync(IdentityApiRoutes.OpenShipClientReconcileScope);
        Assert.Equal(HttpStatusCode.Forbidden, (await LookupAsync(wrongScopeToken, "admin@example.test")).StatusCode);

        // A human administrator token never satisfies the workload policy.
        var humanToken = await fixture.CreateFreshMfaAdminTokenAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await LookupAsync(humanToken, "admin@example.test")).StatusCode);

        var anonymous = await fixture.AnonymousClient.GetAsync($"{IdentityApiRoutes.OpenShipConsoleAccess}?email=admin@example.test");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Deactivation_revokes_openiddict_grants_and_queues_a_security_signal()
    {
        var email = $"leaver-{Guid.NewGuid():N}@example.test";
        Guid userId;
        string authorizationId, tokenId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            userId = await CreateUserAsync(scope.ServiceProvider, email);
            (authorizationId, tokenId) = await CreateOpenIddictGrantAsync(scope.ServiceProvider, userId);
        }

        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var response = await session.PutWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}/{userId:D}/deactivate", new { });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verify = fixture.Services.CreateAsyncScope();
        await AssertGrantRevokedAsync(verify.ServiceProvider, authorizationId, tokenId);
        var db = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.True(await db.SecuritySignalOutbox.AsNoTracking().AnyAsync(item =>
            item.EventType == UserEndpoints.UserDeactivatedSignal && item.Subject == userId.ToString()));
    }

    [Fact]
    public async Task Group_members_are_listed_audited_and_removal_revokes_openiddict_grants()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"member-{suffix}@example.test";
        Guid userId;
        await using (var scope = fixture.Services.CreateAsyncScope())
            userId = await CreateUserAsync(scope.ServiceProvider, email);

        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var root = IdentityApiRoutes.AdminIam;
        var iamScope = await session.PostWithCookiesAsync($"{root}/scopes", new { key = $"tenant-members-{suffix}", displayName = "Members tenant", kind = "tenant", parentId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Created, iamScope.StatusCode);
        var scopeId = (await iamScope.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var group = await session.PostWithCookiesAsync($"{root}/groups", new { key = $"openship-console-read-{suffix}", displayName = "Console read", scopeId });
        Assert.Equal(HttpStatusCode.Created, group.StatusCode);
        var groupId = (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, (await session.PostWithCookiesAsync($"{root}/groups/{groupId:D}/members/{userId:D}")).StatusCode);

        var members = await session.GetWithCookiesAsync($"{root}/groups/{groupId:D}/members");
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
        var member = Assert.Single((await members.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.Equal(userId, member.GetProperty("userId").GetGuid());
        Assert.Equal(email, member.GetProperty("email").GetString());
        Assert.Equal("Member Console", member.GetProperty("displayName").GetString());
        Assert.Equal(IdentityTestData.AdminId.ToString(), member.GetProperty("createdBy").GetString(), ignoreCase: true);
        Assert.True(member.TryGetProperty("createdAt", out _));
        Assert.Equal(HttpStatusCode.NotFound, (await session.GetWithCookiesAsync($"{root}/groups/{Guid.NewGuid():D}/members")).StatusCode);

        string authorizationId, tokenId;
        await using (var scope = fixture.Services.CreateAsyncScope())
            (authorizationId, tokenId) = await CreateOpenIddictGrantAsync(scope.ServiceProvider, userId);

        Assert.Equal(HttpStatusCode.NoContent, (await session.DeleteWithCookiesAsync($"{root}/groups/{groupId:D}/members/{userId:D}")).StatusCode);

        await using var verify = fixture.Services.CreateAsyncScope();
        await AssertGrantRevokedAsync(verify.ServiceProvider, authorizationId, tokenId);
        var db = verify.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.True(await db.AuditLogs.AsNoTracking().AnyAsync(item => item.Action == "AUTHZ_IAM_GROUP_MEMBER_ADD" && item.AfterJson!.Contains(userId.ToString())));
        Assert.True(await db.AuditLogs.AsNoTracking().AnyAsync(item => item.Action == "AUTHZ_IAM_GROUP_MEMBER_REMOVE" && item.BeforeJson!.Contains(userId.ToString())));
        Assert.Empty(await db.IamGroupMemberships.AsNoTracking().Where(item => item.GroupId == groupId).ToListAsync());
    }

    private async Task<HttpResponseMessage> LookupAsync(string token, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{IdentityApiRoutes.OpenShipConsoleAccess}?email={Uri.EscapeDataString(email)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await fixture.AnonymousClient.SendAsync(request);
    }

    private static async Task<Guid> CreateUserAsync(IServiceProvider services, string email, bool isActive = true)
    {
        var userManager = services.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Console",
            LastName = "Member",
            IsActive = isActive
        };
        var result = await userManager.CreateAsync(user, "ConsoleAccess!12345");
        Assert.True(result.Succeeded, string.Join(';', result.Errors.Select(error => error.Code)));
        return user.Id;
    }

    private static async Task<(string AuthorizationId, string TokenId)> CreateOpenIddictGrantAsync(IServiceProvider services, Guid userId)
    {
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
        var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            Subject = userId.ToString(),
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            CreationDate = DateTimeOffset.UtcNow
        });
        var authorizationId = (await authorizations.GetIdAsync(authorization))!;
        var token = await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            Subject = userId.ToString(),
            AuthorizationId = authorizationId,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeHints.RefreshToken,
            CreationDate = DateTimeOffset.UtcNow,
            ExpirationDate = DateTimeOffset.UtcNow.AddDays(1)
        });
        return (authorizationId, (await tokens.GetIdAsync(token))!);
    }

    private static async Task AssertGrantRevokedAsync(IServiceProvider services, string authorizationId, string tokenId)
    {
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
        var authorization = await authorizations.FindByIdAsync(authorizationId);
        var token = await tokens.FindByIdAsync(tokenId);
        Assert.NotNull(authorization);
        Assert.NotNull(token);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, await authorizations.GetStatusAsync(authorization!));
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, await tokens.GetStatusAsync(token!));
    }

    private async Task<string> CreateWorkloadTokenAsync(string scopeName)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var clientId = $"openship-console-gateway-{suffix}";
        var clientSecret = $"secret-{Guid.NewGuid():N}{Guid.NewGuid():N}";

        await using (var seed = fixture.Services.CreateAsyncScope())
        {
            var audience = seed.ServiceProvider.GetRequiredService<IConfiguration>()["Jwt:Audience"];
            Assert.False(string.IsNullOrWhiteSpace(audience));
            var db = seed.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var scope = new IamScope { Key = $"tenant-gateway-{suffix}", DisplayName = "Console gateway tenant", Kind = "tenant" };
            db.IamScopes.Add(scope);
            db.IamWorkloadRoles.Add(new IamWorkloadRole
            {
                Key = clientId,
                DisplayName = "OpenShip console gateway",
                ScopeId = scope.Id,
                Audience = audience!,
                TrustPolicyJson = JsonSerializer.Serialize(new { principals = new[] { clientId } }),
                PermissionsJson = "[]",
                MaxSessionSeconds = 900
            });
            await db.SaveChangesAsync();

            var scopes = seed.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
            if (await scopes.FindByNameAsync(scopeName) is null)
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
                DisplayName = "OpenShip console gateway"
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
