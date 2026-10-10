using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Testing;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class IdentityWorkbenchDedicatedCoverageTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Dedicated_analyzers_cover_validation_unknown_principals_and_normalized_diff()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var invalidRevocation = await session.PostWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.Revocations, new
        {
            principalId = Guid.Empty,
            principalType = "unsupported",
            reason = " "
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRevocation.StatusCode);

        var invalidPrincipalType = await session.PostWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.Revocations, new
        {
            principalId = IdentityTestData.AdminId,
            principalType = "service",
            reason = "coverage"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPrincipalType.StatusCode);

        var missingHuman = await session.PostWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.Revocations, new
        {
            principalId = Guid.NewGuid(),
            principalType = "human",
            reason = "coverage"
        });
        Assert.Equal(HttpStatusCode.NotFound, missingHuman.StatusCode);

        var missingWorkload = await session.PostWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.Revocations, new
        {
            principalId = Guid.NewGuid(),
            principalType = "workload",
            reason = "coverage"
        });
        Assert.Equal(HttpStatusCode.NotFound, missingWorkload.StatusCode);

        var missingAccess = await session.GetWithCookiesAsync($"{IdentityApiRoutes.IdentityWorkbench.EffectiveAccess}/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.NotFound, missingAccess.StatusCode);

        var invalidSimulation = await session.PostWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.PolicySimulator, new
        {
            userId = Guid.Empty,
            permissionCode = " "
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidSimulation.StatusCode);

        var unknownSimulation = await session.PostWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.PolicySimulator, new
        {
            userId = Guid.NewGuid(),
            permissionCode = " users.read "
        });
        Assert.Equal(HttpStatusCode.OK, unknownSimulation.StatusCode);

        var diff = await session.PostWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.AccessDiff, new
        {
            before = new[] { " Users.Read ", "billing.view", "" },
            after = new[] { "users.read", " Billing.Pay ", "billing.view", " " }
        });
        Assert.Equal(HttpStatusCode.OK, diff.StatusCode);
        var diffBody = await diff.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "billing.pay" }, diffBody.GetProperty("added").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(new[] { "billing.view", "users.read" }, diffBody.GetProperty("unchanged").EnumerateArray().Select(x => x.GetString()).Order().ToArray());
        Assert.Empty(diffBody.GetProperty("removed").EnumerateArray());
    }

    [Fact]
    public async Task Dedicated_workload_routes_cover_role_missing_session_and_workload_revocation()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        var suffix = Guid.NewGuid().ToString("N");

        var scope = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/scopes", new
        {
            key = $"dedicated-{suffix}",
            displayName = "Dedicated coverage scope",
            kind = "tenant",
            parentId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.Created, scope.StatusCode);
        var scopeId = (await scope.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var role = await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/workload-roles", new
        {
            key = $"dedicated-role-{suffix}",
            displayName = "Dedicated coverage role",
            scopeId,
            audience = $"api://dedicated-{suffix}",
            trustPolicyJson = "{\"principals\":[\"client\"]}",
            permissions = new[] { "patients.view" },
            maxSessionSeconds = 900
        });
        Assert.Equal(HttpStatusCode.Created, role.StatusCode);
        var roleId = (await role.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var sessions = await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.WorkloadSessions);
        Assert.Equal(HttpStatusCode.OK, sessions.StatusCode);
        Assert.Contains("iam-workload-sessions.v1", await sessions.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NotFound,
            (await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.IdentityWorkbench.WorkloadSessions}/{Guid.NewGuid():D}/missing")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.IdentityWorkbench.WorkloadSessions}/{roleId:D}/missing")).StatusCode);

        var revoke = await session.PostWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.Revocations, new
        {
            principalId = roleId,
            principalType = " WORKLOAD ",
            reason = " Dedicated coverage "
        });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        var revokeBody = await revoke.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("workload", revokeBody.GetProperty("principalType").GetString());
        Assert.Equal($"api://dedicated-{suffix}", revokeBody.GetProperty("subject").GetString());

        var effective = await session.GetWithCookiesAsync($"{IdentityApiRoutes.IdentityWorkbench.EffectiveAccess}/{roleId:D}");
        Assert.Equal(HttpStatusCode.OK, effective.StatusCode);
        var effectiveBody = await effective.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(roleId, effectiveBody.GetProperty("principalId").GetGuid());

        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync($"{IdentityApiRoutes.AdminIam}/workload-roles/{roleId:D}/deactivate")).StatusCode);
    }

    [Fact]
    public async Task Iam_read_projections_cover_external_identity_filters_and_empty_queries()
    {
        using var session = await fixture.CreateAuthenticatedSessionAsync();
        foreach (var route in new[] { IdentityApiRoutes.IdentityWorkbench.Scopes, IdentityApiRoutes.IdentityWorkbench.Services, IdentityApiRoutes.IdentityWorkbench.PermissionSets, IdentityApiRoutes.IdentityWorkbench.Assignments, IdentityApiRoutes.IdentityWorkbench.WorkloadRoles, IdentityApiRoutes.IdentityWorkbench.Groups, IdentityApiRoutes.IdentityWorkbench.Boundaries, IdentityApiRoutes.IdentityWorkbench.ResourcePolicies })
            Assert.Equal(HttpStatusCode.OK, (await session.GetWithCookiesAsync(route)).StatusCode);

        var external = await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.ExternalIdentities);
        Assert.Equal(HttpStatusCode.OK, external.StatusCode);
        var externalBody = await external.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("iam-external-identities.v1", externalBody.GetProperty("schemaVersion").GetString());

        var servicePrincipals = await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.ServicePrincipals);
        Assert.Equal(HttpStatusCode.OK, servicePrincipals.StatusCode);
        Assert.True((await servicePrincipals.Content.ReadFromJsonAsync<JsonElement>()).ValueKind == JsonValueKind.Array);

        var audiences = await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.ApiAudiences);
        Assert.Equal(HttpStatusCode.OK, audiences.StatusCode);
        var issuers = await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.TrustedIssuers);
        Assert.Equal(HttpStatusCode.OK, issuers.StatusCode);

        var revocations = await session.GetWithCookiesAsync($"{IdentityApiRoutes.IdentityWorkbench.Revocations}?limit=0");
        Assert.Equal(HttpStatusCode.OK, revocations.StatusCode);
        var revocationBody = await revocations.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("iam-revocations.v1", revocationBody.GetProperty("schemaVersion").GetString());

        var audit = await session.GetWithCookiesAsync(IdentityApiRoutes.IdentityWorkbench.AuditIntegrations);
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        Assert.Equal("iam-audit-integrations.v1", (await audit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("schemaVersion").GetString());
    }
}
