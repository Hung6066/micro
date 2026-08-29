using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class AccessGovernanceEndpointTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Governance_reads_and_validation_paths_are_exercised_by_admin_session()
    {
        using var session = await LoginAsync();

        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminPolicies)).StatusCode);
        // A signed bundle is fail-closed until the release pipeline publishes
        // one; a fresh integration database must not synthesize an artifact.
        Assert.Equal(HttpStatusCode.NotFound, (await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminPolicies}/bundle")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminAccessRequests)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminAccessReviews)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminBreakGlassRequests)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicies, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminAccessRequests, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminAccessReviews, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminBreakGlassRequests, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicySimulate, new { })).StatusCode);
    }

    [Fact]
    public async Task Identity_workbench_canonical_governance_aliases_are_server_backed()
    {
        using var session = await LoginAsync();

        var routes = new[]
        {
            IdentityApiRoutes.IdentityWorkbench.Policies,
            IdentityApiRoutes.IdentityWorkbench.AccessRequests,
            IdentityApiRoutes.IdentityWorkbench.AccessReviews,
            IdentityApiRoutes.IdentityWorkbench.BreakGlassRequests,
            IdentityApiRoutes.IdentityWorkbench.AuthorizationChanges,
            IdentityApiRoutes.IdentityWorkbench.Sessions,
            IdentityApiRoutes.IdentityWorkbench.AuditLogs
        };

        foreach (var route in routes)
            Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(route)).StatusCode);
    }

    [Fact]
    public async Task Identity_workbench_canonical_identity_and_application_aliases_are_server_backed()
    {
        using var session = await LoginAsync();

        var routes = new[]
        {
            IdentityApiRoutes.IdentityWorkbench.Users,
            IdentityApiRoutes.IdentityWorkbench.Clients,
            IdentityApiRoutes.IdentityWorkbench.ExternalIdentities,
            IdentityApiRoutes.IdentityWorkbench.ServicePrincipals
        };

        foreach (var route in routes)
            Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(route)).StatusCode);
    }

    [Fact]
    public async Task Identity_workbench_dedicated_sessions_revocations_analyzer_and_audit_routes_are_server_backed()
    {
        using var session = await LoginAsync();

        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.WorkloadSessions)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.Revocations)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.AuditIntegrations)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.EffectiveAccess + "/" + Guid.NewGuid().ToString("D"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await session.PostWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.PolicySimulator, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.AccessDiff, new { before = Array.Empty<string>(), after = new[] { "users.read" } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.UnusedPermissions)).StatusCode);
    }

    [Fact]
    public async Task Governance_all_mutating_and_sensitive_routes_reject_anonymous_callers()
    {
        var routes = new[]
        {
            (HttpMethod.Get, IdentityApiRoutes.AdminPolicies),
            (HttpMethod.Get, $"{IdentityApiRoutes.AdminPolicies}/bundle"),
            (HttpMethod.Post, IdentityApiRoutes.AdminPolicies),
            (HttpMethod.Post, IdentityApiRoutes.AdminPolicyCompile(Guid.NewGuid())),
            (HttpMethod.Post, IdentityApiRoutes.AdminPolicySimulate),
            (HttpMethod.Post, IdentityApiRoutes.AdminAccessRequests),
            (HttpMethod.Get, IdentityApiRoutes.AdminAccessRequests),
            (HttpMethod.Post, IdentityApiRoutes.AdminAccessReviews),
            (HttpMethod.Get, IdentityApiRoutes.AdminAccessReviews),
            (HttpMethod.Get, IdentityApiRoutes.AdminBreakGlassRequests),
            (HttpMethod.Post, IdentityApiRoutes.AdminBreakGlassRequests),
            (HttpMethod.Get, IdentityApiRoutes.AdminAuthorizationChangeRequests),
            (HttpMethod.Post, IdentityApiRoutes.AdminAuthorizationChangeRequests),
            (HttpMethod.Post, $"{IdentityApiRoutes.AdminAuthorizationChangeRequests}/{Guid.NewGuid():D}/approve"),
            (HttpMethod.Post, $"{IdentityApiRoutes.AdminAuthorizationChangeRequests}/{Guid.NewGuid():D}/reject"),
            (HttpMethod.Get, IdentityApiRoutes.AdminAuthorizationChanges)
        };

        foreach (var (method, route) in routes)
        {
            using var request = new HttpRequestMessage(method, route);
            if (method != HttpMethod.Get)
                request.Content = JsonContent.Create(new { });

            var response = await fixture.AnonymousClient.SendAsync(request);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });
        }
    }

    [Fact]
    public async Task Authorization_change_requests_require_step_up_for_mutations()
    {
        using var session = await LoginAsync();

        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminAuthorizationChangeRequests)).StatusCode);

        var request = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminAuthorizationChangeRequests, new
        {
            resourceType = "Role",
            resourceId = Guid.NewGuid(),
            action = "role.publish",
            reason = "Validate step-up protection"
        });
        Assert.Equal(HttpStatusCode.Forbidden, request.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await session.PostWithCookiesAsync(
                $"{IdentityApiRoutes.AdminAuthorizationChangeRequests}/{Guid.NewGuid():D}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await session.PostWithCookiesAsync(
                $"{IdentityApiRoutes.AdminAuthorizationChangeRequests}/{Guid.NewGuid():D}/reject", new { })).StatusCode);
    }

    [Fact]
    public async Task Policy_lifecycle_rejects_duplicates_and_unknown_resources()
    {
        using var session = await LoginAsync();
        var key = $"integration-policy-{Guid.NewGuid():N}";
        var create = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicies, new
        {
            key,
            description = "Integration policy for governance validation",
            owner = "identity-tests",
            rulesJson = "{\"requiredFacility\":true}"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var duplicate = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicies, new
        {
            key,
            description = "Duplicate policy",
            rulesJson = "{\"requiredFacility\":true}"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminPolicies}/{id:D}/lint", new { })).StatusCode);
        var compile = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicyCompile(id), new { });
        Assert.Equal(HttpStatusCode.OK, compile.StatusCode);
        var compileBody = await compile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("authorization-policy-compile.v1", compileBody.GetProperty("schemaVersion").GetString());
        Assert.True(compileBody.GetProperty("valid").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(compileBody.GetProperty("hash").GetString()));
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicies + "/" + Guid.NewGuid().ToString("D") + "/lint", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminPolicy(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminEffectiveAccess(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task Policy_update_publish_and_rollback_enforce_state_controls()
    {
        using var session = await LoginAsync();
        var key = $"integration-policy-state-{Guid.NewGuid():N}";
        var create = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicies, new
        {
            key,
            description = "State machine policy",
            owner = "identity-tests",
            rulesJson = "{\"requiredFacility\":true}"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var invalidUpdate = await session.PutWithCookiesAsync(IdentityApiRoutes.AdminPolicy(id), new
        {
            description = "Invalid rules",
            rulesJson = "not-json",
            owner = "identity-tests"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);

        var update = await session.PutWithCookiesAsync(IdentityApiRoutes.AdminPolicy(id), new
        {
            description = "Updated state machine policy",
            // The policy contract uses allowedPurposeOfUse (string array),
            // rather than the evaluator's context field purposeOfUse.
            rulesJson = "{\"requiredFacility\":true,\"allowedPurposeOfUse\":[\"treatment\"]}",
            owner = "identity-tests"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        // Publishing requires a recent MFA assertion; the fixture admin has
        // no assertion, so the endpoint must fail closed.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminPolicy(id)}/publish", new { })).StatusCode);
        // Rollback is also protected by the MFA gate and must fail closed
        // before evaluating whether a previous published version exists.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminPolicy(id)}/rollback", new { })).StatusCode);
    }

    [Fact]
    public async Task Governance_validation_routes_fail_closed_for_invalid_or_unknown_requests()
    {
        using var session = await LoginAsync();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminRebacListObjects, new
            {
                subjectId = Guid.Empty,
                relation = "",
                objectType = ""
            })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminBreakGlassRequests, new { })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicySimulate, new { })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicySimulate, new
            {
                userId = Guid.NewGuid(),
                permissionCode = "users.read"
            })).StatusCode);
    }

    [Fact]
    public async Task Governance_policy_and_incident_mutations_reject_invalid_or_unknown_resources()
    {
        using var session = await LoginAsync();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicies, new
            {
                key = " ", description = " ", rulesJson = "{\"requiredFacility\":true}"
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicies, new
            {
                key = new string('x', 129), description = "Too long policy key", rulesJson = "{\"requiredFacility\":true}"
            })).StatusCode);

        var unknown = Guid.NewGuid();
        var update = await session.PutWithCookiesAsync(IdentityApiRoutes.AdminPolicy(unknown), new
        {
            description = "Unknown policy update", rulesJson = "{\"requiredFacility\":true}", owner = "identity-tests"
        });
        var publish = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminPolicy(unknown)}/publish", new { });
        var rollback = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminPolicy(unknown)}/rollback", new { });
        var reject = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminAccessRequest(unknown)}/reject", new { });
        var revoke = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminBreakGlassRequest(unknown)}/revoke", new { });

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rollback.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reject.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
    }

    [Fact]
    public async Task Policy_simulation_rejects_unknown_published_policy_for_an_existing_user()
    {
        using var session = await LoginAsync();

        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicySimulate, new
        {
            userId = IdentityTestData.AdminId,
            permissionCode = "users.read",
            policyKey = $"missing-policy-{Guid.NewGuid():N}"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Policy_lint_and_compile_report_invalid_persisted_rules_without_throwing()
    {
        using var session = await LoginAsync();
        var key = $"integration-policy-invalid-{Guid.NewGuid():N}";
        var create = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicies, new
        {
            key,
            description = "Persisted invalid policy regression",
            rulesJson = "{\"requiredFacility\":true}"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var policy = await db.AuthorizationPolicies.SingleAsync(item => item.Id == id);
            policy.RulesJson = "not-json";
            await db.SaveChangesAsync();
        }

        var lint = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminPolicy(id)}/lint", new { });
        var compile = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicyCompile(id), new { });
        Assert.Equal(HttpStatusCode.OK, lint.StatusCode);
        Assert.Equal(HttpStatusCode.OK, compile.StatusCode);
        Assert.False((await lint.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("valid").GetBoolean());
        var compileBody = await compile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(compileBody.GetProperty("valid").GetBoolean());
        Assert.Equal(JsonValueKind.Null, compileBody.GetProperty("artifact").ValueKind);
    }

    [Fact]
    public async Task Policy_create_defaults_owner_and_rebac_valid_requests_fail_closed()
    {
        using var session = await LoginAsync();
        var create = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicies, new
        {
            key = $"integration-policy-default-owner-{Guid.NewGuid():N}",
            description = "Default owner policy",
            rulesJson = "{\"requiredFacility\":true}"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("identity-service", created.GetProperty("owner").GetString());

        var rebac = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminRebacListObjects, new
        {
            subjectId = IdentityTestData.AdminId,
            relation = "viewer",
            objectType = "User"
        });
        Assert.Contains(rebac.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable });
    }

    [Fact]
    public async Task Access_request_rejection_and_break_glass_revocation_are_audited_lifecycles()
    {
        using var session = await LoginAsync();

        var users = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}?page=1&pageSize=1&isActive=true");
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        var usersBody = await users.Content.ReadFromJsonAsync<JsonElement>();
        var subjectUserId = usersBody.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var roles = await session.GetWithCookiesAsync(IdentityApiRoutes.Roles);
        Assert.Equal(HttpStatusCode.OK, roles.StatusCode);
        var rolesBody = await roles.Content.ReadFromJsonAsync<JsonElement>();
        var roleId = rolesBody.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var accessRequest = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminAccessRequests, new
        {
            subjectUserId,
            roleIds = new[] { roleId.ToString("D") },
            reason = "Temporary governance review for integration coverage",
            expiryHours = 24
        });
        Assert.Equal(HttpStatusCode.Created, accessRequest.StatusCode);
        var accessRequestBody = await accessRequest.Content.ReadFromJsonAsync<JsonElement>();
        var accessRequestId = accessRequestBody.GetProperty("id").GetGuid();

        var rejected = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminAccessRequests}/{accessRequestId:D}/reject", new { });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("rejected", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminAccessRequests}/{accessRequestId:D}/reject", new { })).StatusCode);

        var breakGlass = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminBreakGlassRequests, new
        {
            subjectUserId,
            permissionCode = "admin.users.read",
            facilityId = "facility-a",
            reason = "Emergency read-only access audit coverage",
            durationMinutes = 10,
            resourceType = "User",
            resourceId = subjectUserId.ToString("D")
        });
        Assert.Equal(HttpStatusCode.Created, breakGlass.StatusCode);
        var breakGlassBody = await breakGlass.Content.ReadFromJsonAsync<JsonElement>();
        var breakGlassId = breakGlassBody.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminBreakGlassRequests}/{breakGlassId:D}/revoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminBreakGlassRequests}/{breakGlassId:D}/revoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.AdminBreakGlassRequests)).StatusCode);
    }

    [Fact]
    public async Task Policy_simulation_returns_deterministic_deny_and_effective_access_shape()
    {
        using var session = await LoginAsync();
        var users = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}?page=1&pageSize=1&isActive=true");
        var usersBody = await users.Content.ReadFromJsonAsync<JsonElement>();
        var subjectUserId = usersBody.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var simulation = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicySimulate, new
        {
            userId = subjectUserId,
            permissionCode = "permission.not.granted",
            facilityId = "facility-that-does-not-match"
        });
        Assert.Equal(HttpStatusCode.OK, simulation.StatusCode);
        var simulationBody = await simulation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("deny", simulationBody.GetProperty("decision").GetString());

        var effective = await session.GetWithCookiesAsync(IdentityApiRoutes.AdminEffectiveAccess(subjectUserId));
        Assert.Equal(HttpStatusCode.OK, effective.StatusCode);
        var effectiveBody = await effective.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(subjectUserId, effectiveBody.GetProperty("userId").GetGuid());
        Assert.True(effectiveBody.TryGetProperty("roles", out _));
        Assert.True(effectiveBody.TryGetProperty("permissions", out _));
        Assert.True(effectiveBody.TryGetProperty("facilityIds", out _));
    }

    [Fact]
    public async Task Policy_bundle_and_validation_artifacts_expose_stable_contracts()
    {
        using var session = await LoginAsync();

        var bundle = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminPolicies}/bundle");
        // The endpoint deliberately returns 404 when no durable signed
        // artifact has been published for this database.
        Assert.Equal(HttpStatusCode.NotFound, bundle.StatusCode);
        // The response body is intentionally not part of the fail-closed
        // contract; status is the stable signal consumed by the release gate.

        var key = $"integration-policy-artifact-{Guid.NewGuid():N}";
        var create = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicies, new
        {
            key,
            description = "Policy artifact contract coverage",
            rulesJson = "{\"requiredFacility\":true}"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var lint = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminPolicies}/{id:D}/lint", new { });
        Assert.Equal(HttpStatusCode.OK, lint.StatusCode);
        var lintBody = await lint.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(lintBody.GetProperty("valid").GetBoolean());
        Assert.Equal(JsonValueKind.Array, lintBody.GetProperty("errors").ValueKind);

        var compile = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminPolicyCompile(id), new { });
        Assert.Equal(HttpStatusCode.OK, compile.StatusCode);
        var compileBody = await compile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("authorization-policy-compile.v1", compileBody.GetProperty("schemaVersion").GetString());
        Assert.True(compileBody.GetProperty("valid").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(compileBody.GetProperty("artifact").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(compileBody.GetProperty("hash").GetString()));
    }

    [Fact]
    public async Task Access_request_and_review_mfa_transitions_fail_closed()
    {
        using var session = await LoginAsync();
        var users = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}?page=1&pageSize=1&isActive=true");
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        var subjectUserId = (await users.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var roles = await session.GetWithCookiesAsync(IdentityApiRoutes.Roles);
        Assert.Equal(HttpStatusCode.OK, roles.StatusCode);
        var roleId = (await roles.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var unknownRole = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminAccessRequests, new
        {
            subjectUserId,
            roleIds = new[] { Guid.NewGuid().ToString("D") },
            reason = "Unknown role validation coverage"
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknownRole.StatusCode);

        var request = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminAccessRequests, new
        {
            subjectUserId,
            roleIds = new[] { roleId.ToString("D") },
            reason = "MFA approval gate coverage request"
        });
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var requestId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminAccessRequests}/{requestId:D}/approve", new { })).StatusCode);

        var review = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminAccessReviews, new
        {
            subjectUserId,
            roleIds = new[] { roleId.ToString("D") },
            dueDays = 30
        });
        Assert.Equal(HttpStatusCode.Created, review.StatusCode);
        var reviewId = (await review.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminAccessReviews}/{reviewId:D}/certify", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminAccessReviews}/{reviewId:D}/revoke", new { })).StatusCode);
    }

    [Fact]
    public async Task Break_glass_invalid_permission_and_approval_require_mfa()
    {
        using var session = await LoginAsync();
        var users = await session.GetWithCookiesAsync($"{IdentityApiRoutes.AdminUsers}?page=1&pageSize=1&isActive=true");
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        var subjectUserId = (await users.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var invalidPermission = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminBreakGlassRequests, new
        {
            subjectUserId,
            permissionCode = "permission.not.registered",
            facilityId = "facility-a",
            reason = "Invalid permission catalog coverage",
            durationMinutes = 10
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPermission.StatusCode);

        var create = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminBreakGlassRequests, new
        {
            subjectUserId,
            permissionCode = "admin.users.read",
            facilityId = "facility-a",
            reason = "MFA approval gate coverage request",
            durationMinutes = 10
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminBreakGlassRequests}/{id:D}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminBreakGlassRequests}/{id:D}/revoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminBreakGlassRequests}/{id:D}/revoke", new { })).StatusCode);
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }
}
