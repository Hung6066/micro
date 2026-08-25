using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class IamControlPlaneEndpointTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public IamControlPlaneEndpointTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Iam_control_plane_rejects_invalid_scope_and_service_commands()
    {
        using var session = await _fixture.CreateAuthenticatedSessionAsync();
        var root = IdentityApiRoutes.AdminIam;

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync($"{root}/scopes", new { key = "", displayName = "", kind = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync($"{root}/scopes", new { key = "child", displayName = "Child", kind = "tenant", parentId = Guid.NewGuid() })).StatusCode);

        var servicePayload = new { key = $"invalid-branch-{Guid.NewGuid():N}", displayName = "Invalid branch", permissionPrefix = "invalid", owner = "tests" };
        Assert.Equal(HttpStatusCode.Created,
            (await session.PostWithCookiesAsync($"{root}/services", servicePayload)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{root}/services", servicePayload)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync($"{root}/services", new { key = "missing-prefix", displayName = "Missing", permissionPrefix = "", owner = "tests" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PutWithCookiesAsync($"{root}/services/{Guid.NewGuid():D}", servicePayload)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync($"{root}/scopes/{Guid.NewGuid():D}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync($"{root}/scopes/{Guid.NewGuid():D}/activate")).StatusCode);
    }

    [Fact]
    public async Task Iam_scope_hierarchy_enforces_parent_kind_and_active_children()
    {
        using var session = await _fixture.CreateAuthenticatedSessionAsync();
        var root = IdentityApiRoutes.AdminIam;
        var suffix = Guid.NewGuid().ToString("N");

        var organization = await session.PostWithCookiesAsync($"{root}/scopes", new
        {
            key = $"org-{suffix}", displayName = "Organization", kind = "organization", parentId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.Created, organization.StatusCode);
        var organizationId = (await organization.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var tenant = await session.PostWithCookiesAsync($"{root}/scopes", new
        {
            key = $"tenant-{suffix}", displayName = "Tenant", kind = "tenant", parentId = organizationId
        });
        Assert.Equal(HttpStatusCode.Created, tenant.StatusCode);
        var tenantId = (await tenant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync($"{root}/scopes", new
            {
                key = $"environment-{suffix}", displayName = "Invalid environment", kind = "environment", parentId = organizationId
            })).StatusCode);
        var account = await session.PostWithCookiesAsync($"{root}/scopes", new
        {
            key = $"account-{suffix}", displayName = "Account", kind = "account", parentId = tenantId
        });
        Assert.Equal(HttpStatusCode.Created, account.StatusCode);
        var accountId = (await account.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var environment = await session.PostWithCookiesAsync($"{root}/scopes", new
            {
                key = $"environment-valid-{suffix}", displayName = "Environment", kind = "environment", parentId = accountId
            });
        Assert.Equal(HttpStatusCode.Created, environment.StatusCode);
        var environmentId = (await environment.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{root}/scopes/{organizationId:D}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync($"{root}/scopes/{environmentId:D}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync($"{root}/scopes/{accountId:D}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync($"{root}/scopes/{tenantId:D}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync($"{root}/scopes/{organizationId:D}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync($"{root}/scopes/{organizationId:D}/activate")).StatusCode);
    }

    [Fact]
    public async Task Iam_control_plane_requires_authentication()
    {
        foreach (var route in new[]
        {
            $"{IdentityApiRoutes.AdminIam}/overview",
            $"{IdentityApiRoutes.AdminIam}/scopes",
            $"{IdentityApiRoutes.AdminIam}/services",
            $"{IdentityApiRoutes.AdminIam}/permission-sets",
            $"{IdentityApiRoutes.AdminIam}/workload-roles"
        })
        {
            var response = await _fixture.AnonymousClient.GetAsync(route);
            Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect, route);
        }
    }

    [Fact]
    public async Task Iam_control_plane_all_read_and_write_routes_reject_anonymous_callers()
    {
        var id = Guid.NewGuid().ToString("D");
        var routes = new[]
        {
            (HttpMethod.Get, "overview"),
            (HttpMethod.Get, "scopes"), (HttpMethod.Post, "scopes"), (HttpMethod.Post, $"scopes/{id}/deactivate"), (HttpMethod.Post, $"scopes/{id}/activate"),
            (HttpMethod.Get, "services"), (HttpMethod.Post, "services"), (HttpMethod.Put, $"services/{id}"), (HttpMethod.Post, $"services/{id}/deactivate"), (HttpMethod.Post, $"services/{id}/activate"),
            (HttpMethod.Get, "permission-sets"), (HttpMethod.Post, "permission-sets"),
            (HttpMethod.Put, $"permission-sets/{id}"),
            (HttpMethod.Get, "assignments"), (HttpMethod.Post, $"permission-sets/{id}/assignments"),
            (HttpMethod.Post, $"permission-sets/{id}/publish"), (HttpMethod.Post, $"assignments/{id}/revoke"),
            (HttpMethod.Get, $"principals/{id}/effective-access"),
            (HttpMethod.Get, "workload-roles"), (HttpMethod.Post, "workload-roles"), (HttpMethod.Put, $"workload-roles/{id}"), (HttpMethod.Post, $"workload-roles/{id}/deactivate"), (HttpMethod.Post, $"workload-roles/{id}/activate"), (HttpMethod.Post, $"workload-roles/{id}/revoke-sessions"), (HttpMethod.Post, $"workload-roles/{id}/rotate-credential"), (HttpMethod.Get, $"workload-roles/{id}/sessions"), (HttpMethod.Delete, $"workload-roles/{id}/sessions/session"),
            (HttpMethod.Get, "groups"), (HttpMethod.Post, "groups"), (HttpMethod.Put, $"groups/{id}"), (HttpMethod.Post, $"groups/{id}/deactivate"), (HttpMethod.Post, $"groups/{id}/activate"),
            (HttpMethod.Post, $"groups/{id}/members/{id}"), (HttpMethod.Delete, $"groups/{id}/members/{id}"),
            (HttpMethod.Get, "boundaries"), (HttpMethod.Post, "boundaries"),
            (HttpMethod.Put, $"boundaries/{id}"), (HttpMethod.Post, $"boundaries/{id}/deactivate"), (HttpMethod.Post, $"boundaries/{id}/activate"),
            (HttpMethod.Get, "resource-policies"), (HttpMethod.Post, "resource-policies"), (HttpMethod.Put, $"resource-policies/{id}"),
            (HttpMethod.Post, $"resource-policies/{id}/publish"),
            (HttpMethod.Post, "analyzer"), (HttpMethod.Post, "analyzer/new-access-diff"),
            (HttpMethod.Get, "analyzer/unused")
        };

        foreach (var (method, suffix) in routes)
        {
            using var request = new HttpRequestMessage(method, $"{IdentityApiRoutes.AdminIam}/{suffix}");
            if (method != HttpMethod.Get && method != HttpMethod.Delete)
                request.Content = JsonContent.Create(new { });

            var response = await _fixture.AnonymousClient.SendAsync(request);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });
        }
    }

    [Fact]
    public async Task Admin_can_read_iam_overview_summary()
    {
        using var session = _fixture.CreateSessionClient();
        var login = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var response = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/overview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("iam-overview.v1", body, StringComparison.Ordinal);
        Assert.Contains("publishedPermissionSets", body, StringComparison.Ordinal);
        Assert.Contains("pendingBreakGlass", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Admin_external_identity_catalog_only_publishes_valid_configured_providers()
    {
        var configuration = _fixture.Services.GetRequiredService<IConfiguration>();
        var keys = new[]
        {
            "Authentication:Google:ClientId", "Authentication:Microsoft:ClientId",
            "Authentication:Entra:ClientId", "Authentication:Entra:Authority",
            "Authentication:ExternalSources:0:Name", "Authentication:ExternalSources:0:DisplayName",
            "Authentication:ExternalSources:0:Authority"
        };
        var previous = keys.ToDictionary(key => key, key => configuration[key]);
        try
        {
            configuration["Authentication:Google:ClientId"] = "google-client";
            configuration["Authentication:Microsoft:ClientId"] = "microsoft-client";
            configuration["Authentication:Entra:ClientId"] = "entra-client";
            configuration["Authentication:Entra:Authority"] = "https://login.example.test/tenant";
            configuration["Authentication:ExternalSources:0:Name"] = "partner";
            configuration["Authentication:ExternalSources:0:DisplayName"] = "Partner SSO";
            configuration["Authentication:ExternalSources:0:Authority"] = "https://partner.example.test";

            using var session = await _fixture.CreateAuthenticatedSessionAsync();
            var response = await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.ExternalIdentities);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var providers = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("providers");
            Assert.Contains(providers.EnumerateArray(), item => item.GetProperty("provider").GetString() == "Google");
            Assert.Contains(providers.EnumerateArray(), item => item.GetProperty("provider").GetString() == "Microsoft");
            Assert.Contains(providers.EnumerateArray(), item => item.GetProperty("provider").GetString() == "Entra");
            Assert.Contains(providers.EnumerateArray(), item => item.GetProperty("provider").GetString() == "partner");
        }
        finally
        {
            foreach (var pair in previous) configuration[pair.Key] = pair.Value;
        }
    }

    [Fact]
    public async Task Admin_iam_read_models_expose_service_scope_audience_and_issuer_contracts()
    {
        using var session = await _fixture.CreateAuthenticatedSessionAsync();
        var root = IdentityApiRoutes.AdminIam;
        foreach (var suffix in new[] { "service-principals", "scopes", "services", "api-audiences", "trusted-issuers" })
        {
            var response = await session.GetWithCookiesAsync($"{root}/{suffix}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Admin_can_create_publish_assign_and_revoke_permission_set()
    {
        using var session = _fixture.CreateSessionClient();
        var login = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var suffix = Guid.NewGuid().ToString("N");
        var scopeResponse = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/scopes", new
        {
            key = $"tenant-{suffix}",
            displayName = "Integration tenant",
            kind = "tenant",
            parentId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.Created, scopeResponse.StatusCode);
        var scope = await scopeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var scopeId = scope.GetProperty("id").GetGuid();

        var serviceResponse = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/services", new
        {
            key = $"integration-{suffix}",
            displayName = "Integration service",
            permissionPrefix = "integration",
            owner = "identity-service"
        });
        Assert.Equal(HttpStatusCode.Created, serviceResponse.StatusCode);

        var setResponse = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets", new
        {
            key = $"integration-read-{suffix}",
            displayName = "Integration read",
            scopeId,
            permissions = new[] { "patients.view" }
        });
        Assert.Equal(HttpStatusCode.Created, setResponse.StatusCode);
        var set = await setResponse.Content.ReadFromJsonAsync<JsonElement>();
        var setId = set.GetProperty("id").GetGuid();

        var update = await session.PutWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets/{setId:D}", new
        {
            key = $"integration-read-updated-{suffix}", displayName = "Integration read updated", scopeId,
            permissions = new[] { "patients.view", "patients.update" }
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updateBody = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("draft", updateBody.GetProperty("lifecycleStatus").GetString());

        var draftAssignment = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets/{setId:D}/assignments", new
        {
            principalId = Guid.NewGuid(), principalType = "human", scopeId, expiresAt = (DateTime?)null
        });
        Assert.Equal(HttpStatusCode.Conflict, draftAssignment.StatusCode);

        var publish = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets/{setId:D}/publish");
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var usersResponse = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}?page=1&pageSize=1&isActive=true");
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        var usersBody = await usersResponse.Content.ReadFromJsonAsync<JsonElement>();
        var principalId = usersBody.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var assignment = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets/{setId:D}/assignments", new
        {
            principalId,
            principalType = "human",
            scopeId,
            expiresAt = (DateTime?)null
        });
        Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);
        var assignmentBody = await assignment.Content.ReadFromJsonAsync<JsonElement>();
        var assignmentId = assignmentBody.GetProperty("id").GetGuid();

        var effective = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/principals/{principalId:D}/effective-access?scopeId={scopeId:D}");
        Assert.Equal(HttpStatusCode.OK, effective.StatusCode);
        var effectiveBody = await effective.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("patients.view", effectiveBody.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()));

        var revoke = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/assignments/{assignmentId:D}/revoke");
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
    }

    [Fact]
    public async Task Permission_set_rejects_permission_outside_server_catalog()
    {
        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);

        var scopeResponse = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/scopes", new
        {
            key = $"tenant-{Guid.NewGuid():N}", displayName = "Catalog test tenant", kind = "tenant", parentId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.Created, scopeResponse.StatusCode);
        var scope = await scopeResponse.Content.ReadFromJsonAsync<JsonElement>();

        var response = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets", new
        {
            key = $"invalid-{Guid.NewGuid():N}", displayName = "Invalid permission set", scopeId = scope.GetProperty("id").GetGuid(),
            permissions = new[] { "unknown.service.action" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_group_boundary_resource_policy_and_analyzer_views()
    {
        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var suffix = Guid.NewGuid().ToString("N");
        var scopeResponse = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/scopes", new { key = $"tenant-{suffix}", displayName = "IAM extension tenant", kind = "tenant", parentId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Created, scopeResponse.StatusCode);
        var scopeId = (await scopeResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using (var seed = _fixture.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.IamPermissionSets.Add(new IamPermissionSet
            {
                Key = $"wildcard-{suffix}", DisplayName = "Wildcard", ScopeId = scopeId,
                PermissionsJson = "[\"*\"]"
            });
            db.IamWorkloadRoles.Add(new IamWorkloadRole
            {
                Key = $"long-session-{suffix}", DisplayName = "Long session", ScopeId = scopeId,
                Audience = "", MaxSessionSeconds = 3600
            });
            await db.SaveChangesAsync();
        }

        var group = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/groups", new { key = $"finance-{suffix}", displayName = "Finance group", scopeId });
        Assert.Equal(HttpStatusCode.Created, group.StatusCode);
        var groupId = (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await session.PutWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/groups/{groupId:D}", new { key = $"finance-{suffix}", displayName = "Finance group updated", scopeId })).StatusCode);
        var usersResponse = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}?page=1&pageSize=1&isActive=true");
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        var usersBody = await usersResponse.Content.ReadFromJsonAsync<JsonElement>();
        var boundaryPrincipalId = usersBody.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var boundary = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/boundaries", new { principalId = boundaryPrincipalId, principalType = "human", scopeId, allowedPermissions = new[] { "billing.view" }, resourceConstraintsJson = "{}" });
        Assert.Equal(HttpStatusCode.Created, boundary.StatusCode);
        var boundaryId = (await boundary.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var service = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/services", new { key = "billing", displayName = "Billing", permissionPrefix = "billing", owner = "integration-test" });
        Assert.Equal(HttpStatusCode.Created, service.StatusCode);
        var serviceId = (await service.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await session.PutWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/services/{serviceId:D}", new { key = "billing", displayName = "Billing updated", permissionPrefix = "billing", owner = "integration-test" })).StatusCode);
        var policy = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/resource-policies", new { scopeId, serviceKey = "billing", resourcePattern = $"invoice/{suffix}/*", statementsJson = "[]" });
        Assert.Equal(HttpStatusCode.Created, policy.StatusCode);
        var policyId = (await policy.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await session.PutWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/resource-policies/{policyId:D}", new { scopeId, serviceKey = "billing", resourcePattern = $"invoice/{suffix}/*", statementsJson = "[{\"effect\":\"allow\"}]" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/resource-policies/{policyId:D}/publish")).StatusCode);
        var diff = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/analyzer/new-access-diff", new { before = new[] { "billing.view" }, after = new[] { "billing.view", "billing.pay" } });
        Assert.Equal(HttpStatusCode.OK, diff.StatusCode);
        var analyzer = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/analyzer", new { });
        Assert.Equal(HttpStatusCode.OK, analyzer.StatusCode);
        var findings = (await analyzer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("findings");
        Assert.Contains(findings.EnumerateArray(), item => item.GetProperty("code").GetString() == "WILDCARD_PERMISSION");
        Assert.Contains(findings.EnumerateArray(), item => item.GetProperty("code").GetString() == "LONG_SESSION");
        Assert.Contains(findings.EnumerateArray(), item => item.GetProperty("code").GetString() == "MISSING_AUDIENCE");
        var unused = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/analyzer/unused");
        Assert.Equal(HttpStatusCode.OK, unused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/services/{serviceId:D}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/boundaries/{boundaryId:D}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/services/{serviceId:D}/activate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/boundaries/{boundaryId:D}/activate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/boundaries/{boundaryId:D}/deactivate")).StatusCode);
    }

    [Fact]
    public async Task Group_membership_contributes_to_effective_access_and_can_be_removed()
    {
        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var suffix = Guid.NewGuid().ToString("N");
        var users = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}?page=1&pageSize=1&isActive=true");
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        var usersBody = await users.Content.ReadFromJsonAsync<JsonElement>();
        var userId = usersBody.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var scope = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/scopes", new { key = $"tenant-{suffix}", displayName = "Group tenant", kind = "tenant", parentId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Created, scope.StatusCode);
        var scopeId = (await scope.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var group = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/groups", new { key = $"group-{suffix}", displayName = "Group membership", scopeId });
        Assert.Equal(HttpStatusCode.Created, group.StatusCode);
        var groupId = (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var set = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets", new { key = $"set-{suffix}", displayName = "Group set", scopeId, permissions = new[] { "patients.view" } });
        Assert.Equal(HttpStatusCode.Created, set.StatusCode);
        var setId = (await set.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets/{setId:D}/publish")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets/{setId:D}/assignments", new { principalId = groupId, principalType = "group", scopeId })).StatusCode);
        var membership = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/groups/{groupId:D}/members/{userId:D}");
        Assert.True(membership.StatusCode == HttpStatusCode.Created, $"membership status={membership.StatusCode}, body={await membership.Content.ReadAsStringAsync()}");
        var effective = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/principals/{userId:D}/effective-access?scopeId={scopeId:D}");
        Assert.Equal(HttpStatusCode.OK, effective.StatusCode);
        var effectiveBody = await effective.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("patients.view", effectiveBody.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/groups/{groupId:D}/deactivate")).StatusCode);
        var deactivatedEffective = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/principals/{userId:D}/effective-access?scopeId={scopeId:D}");
        Assert.DoesNotContain("patients.view", (await deactivatedEffective.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/groups/{groupId:D}/activate")).StatusCode);
        var reactivatedEffective = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/principals/{userId:D}/effective-access?scopeId={scopeId:D}");
        Assert.Contains("patients.view", (await reactivatedEffective.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.NoContent, (await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/groups/{groupId:D}/members/{userId:D}")).StatusCode);
    }

    [Fact]
    public async Task Workload_role_can_be_updated_with_catalog_validation()
    {
        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var suffix = Guid.NewGuid().ToString("N");
        var scope = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/scopes", new { key = $"tenant-{suffix}", displayName = "Workload tenant", kind = "tenant", parentId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Created, scope.StatusCode);
        var scopeId = (await scope.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var create = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/workload-roles", new { key = $"worker-{suffix}", displayName = "Worker", scopeId, audience = $"api://worker-{suffix}", trustPolicyJson = "{\"principals\":[\"client\"]}", permissions = new[] { "patients.view" }, maxSessionSeconds = 900 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var update = await session.PutWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/workload-roles/{id:D}", new { key = $"worker-{suffix}", displayName = "Worker updated", scopeId, audience = $"api://worker-{suffix}", trustPolicyJson = "{\"principals\":[\"client\"]}", permissions = new[] { "patients.view", "patients.update" }, maxSessionSeconds = 1200 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var body = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Worker updated", body.GetProperty("displayName").GetString());
        Assert.Contains("patients.update", body.GetProperty("permissionsJson").GetString());
        var permissionSet = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets", new
        {
            key = $"worker-set-{suffix}", displayName = "Worker permission set", scopeId,
            permissions = new[] { "patients.view" }
        });
        Assert.Equal(HttpStatusCode.Created, permissionSet.StatusCode);
        var permissionSetId = (await permissionSet.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets/{permissionSetId:D}/publish")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/permission-sets/{permissionSetId:D}/assignments", new
        {
            principalId = id, principalType = "workload", scopeId, expiresAt = (DateTime?)null
        })).StatusCode);
        var effective = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/principals/{id:D}/effective-access?scopeId={scopeId:D}");
        Assert.Equal(HttpStatusCode.OK, effective.StatusCode);
        var effectiveBody = await effective.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("workload", effectiveBody.GetProperty("principalType").GetString());
        Assert.Contains("patients.view", effectiveBody.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("patients.update", effectiveBody.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()));
        var deactivated = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/workload-roles/{id:D}/deactivate");
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.False((await deactivated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isActive").GetBoolean());
        var reactivated = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/workload-roles/{id:D}/activate");
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.True((await reactivated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isActive").GetBoolean());
        var revoked = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/workload-roles/{id:D}/revoke-sessions");
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        var revokedBody = await revoked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(revokedBody.GetProperty("revoked").GetBoolean());
    }

    [Fact]
    public async Task Iam_group_and_boundary_guards_reject_duplicates_missing_principals_and_inactive_scopes()
    {
        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var root = IdentityApiRoutes.AdminIam;
        var suffix = Guid.NewGuid().ToString("N");

        var scope = await session.PostWithCookiesAsync($"{root}/scopes", new
        {
            key = $"tenant-guards-{suffix}", displayName = "IAM guard scope", kind = "tenant", parentId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.Created, scope.StatusCode);
        var scopeId = (await scope.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var group = await session.PostWithCookiesAsync($"{root}/groups", new
        {
            key = $"group-guards-{suffix}", displayName = "IAM guard group", scopeId
        });
        Assert.Equal(HttpStatusCode.Created, group.StatusCode);
        var groupId = (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{root}/groups", new
            {
                key = $"group-guards-{suffix}", displayName = "Duplicate group", scopeId
            })).StatusCode);

        var member = await session.PostWithCookiesAsync($"{root}/groups/{groupId:D}/members/{IdentityTestData.AdminId:D}");
        Assert.Equal(HttpStatusCode.Created, member.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{root}/groups/{groupId:D}/members/{IdentityTestData.AdminId:D}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync($"{root}/groups/{Guid.NewGuid():D}/members/{IdentityTestData.AdminId:D}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.DeleteWithCookiesAsync($"{root}/groups/{groupId:D}/members/{Guid.NewGuid():D}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.DeleteWithCookiesAsync($"{root}/groups/{groupId:D}/members/{IdentityTestData.AdminId:D}")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync($"{root}/boundaries", new
            {
                principalId = IdentityTestData.AdminId, principalType = "unsupported", scopeId,
                allowedPermissions = new[] { "patients.view" }, resourceConstraintsJson = "{}"
            })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync($"{root}/boundaries", new
            {
                principalId = Guid.NewGuid(), principalType = "human", scopeId,
                allowedPermissions = new[] { "patients.view" }, resourceConstraintsJson = "{}"
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync($"{root}/boundaries", new
            {
                principalId = IdentityTestData.AdminId, principalType = "human", scopeId,
                allowedPermissions = new[] { "patients.view" }, resourceConstraintsJson = "[]"
            })).StatusCode);

        var boundary = await session.PostWithCookiesAsync($"{root}/boundaries", new
        {
            principalId = IdentityTestData.AdminId, principalType = "human", scopeId,
            allowedPermissions = new[] { "patients.view" }, resourceConstraintsJson = "{}"
        });
        Assert.Equal(HttpStatusCode.Created, boundary.StatusCode);
        var boundaryId = (await boundary.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{root}/boundaries", new
            {
                principalId = IdentityTestData.AdminId, principalType = "human", scopeId,
                allowedPermissions = new[] { "patients.view" }, resourceConstraintsJson = "{}"
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PutWithCookiesAsync($"{root}/boundaries/{boundaryId:D}", new
            {
                allowedPermissions = new[] { "patients.view" }, resourceConstraintsJson = "[]", isActive = true
            })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{root}/boundaries/{boundaryId:D}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{root}/scopes/{scopeId:D}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await session.PostWithCookiesAsync($"{root}/boundaries/{boundaryId:D}/activate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{root}/scopes/{scopeId:D}/activate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync($"{root}/boundaries/{boundaryId:D}/activate")).StatusCode);

        // This boundary intentionally targets the canonical admin principal to
        // exercise authorization filtering. Remove it before the next test so
        // the shared fixture admin does not retain a patients-only permission
        // boundary and make unrelated endpoint tests fail with 403.
        await using (var cleanupScope = _fixture.Services.CreateAsyncScope())
        {
            var db = cleanupScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var cleanupBoundary = await db.IamPermissionBoundaries.SingleOrDefaultAsync(item => item.Id == boundaryId);
            if (cleanupBoundary is not null) db.IamPermissionBoundaries.Remove(cleanupBoundary);
            var cleanupGroup = await db.IamGroups.SingleOrDefaultAsync(item => item.Id == groupId);
            if (cleanupGroup is not null) db.IamGroups.Remove(cleanupGroup);
            var cleanupIamScope = await db.IamScopes.SingleOrDefaultAsync(item => item.Id == scopeId);
            if (cleanupIamScope is not null) db.IamScopes.Remove(cleanupIamScope);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Iam_workload_and_resource_policy_inputs_reject_invalid_json_and_unknown_dependencies()
    {
        using var session = _fixture.CreateSessionClient();
        Assert.Equal(HttpStatusCode.OK, (await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password)).StatusCode);
        var root = IdentityApiRoutes.AdminIam;
        var suffix = Guid.NewGuid().ToString("N");
        var scope = await session.PostWithCookiesAsync($"{root}/scopes", new
        {
            key = $"tenant-inputs-{suffix}", displayName = "IAM input scope", kind = "tenant", parentId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.Created, scope.StatusCode);
        var scopeId = (await scope.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync($"{root}/workload-roles", new
            {
                key = $"invalid-workload-{suffix}", displayName = "Invalid workload", scopeId,
                audience = $"api://invalid-{suffix}", trustPolicyJson = "{\"principals\":[]}", permissions = new[] { "patients.view" }, maxSessionSeconds = 900
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync($"{root}/workload-roles", new
            {
                key = $"invalid-session-{suffix}", displayName = "Invalid session", scopeId,
                audience = $"api://invalid-session-{suffix}", trustPolicyJson = "{\"principals\":[\"client\"]}", permissions = new[] { "patients.view" }, maxSessionSeconds = 30
            })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync($"{root}/workload-roles", new
            {
                key = $"missing-scope-{suffix}", displayName = "Missing scope", scopeId = Guid.NewGuid(),
                audience = $"api://missing-{suffix}", trustPolicyJson = "{\"principals\":[\"client\"]}", permissions = new[] { "patients.view" }, maxSessionSeconds = 900
            })).StatusCode);

        var service = await session.PostWithCookiesAsync($"{root}/services", new
        {
            key = $"service-inputs-{suffix}", displayName = "IAM input service", permissionPrefix = $"inputs{suffix[..8]}", owner = "identity-tests"
        });
        Assert.Equal(HttpStatusCode.Created, service.StatusCode);
        var serviceKey = $"service-inputs-{suffix}";
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync($"{root}/resource-policies", new
            {
                scopeId, serviceKey = "missing-service", resourcePattern = "invoice/*", statementsJson = "[]"
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync($"{root}/resource-policies", new
            {
                scopeId, serviceKey, resourcePattern = "invoice/*", statementsJson = "{}"
            })).StatusCode);
        var policy = await session.PostWithCookiesAsync($"{root}/resource-policies", new
        {
            scopeId, serviceKey, resourcePattern = $"invoice/{suffix}/*", statementsJson = "[]"
        });
        Assert.Equal(HttpStatusCode.Created, policy.StatusCode);
        var policyId = (await policy.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{root}/resource-policies", new
            {
                scopeId, serviceKey, resourcePattern = $"invoice/{suffix}/*", statementsJson = "[]"
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PutWithCookiesAsync($"{root}/resource-policies/{policyId:D}", new
            {
                scopeId, serviceKey, resourcePattern = $"invoice/{suffix}/*", statementsJson = "{}"
            })).StatusCode);
    }
}
