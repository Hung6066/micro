using System.Security.Claims;
using FluentAssertions;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Application.OpenIddict;
using His.Hope.IdentityService.Application.OpenShip;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Moq;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class OpenIddictPopulateTokenClaimsTests
{
    [Fact]
    public async Task Returns_without_mutation_when_principal_is_missing()
    {
        await using var db = TestApplicationDbContext.Create();
        var context = Context(OpenIddictConstants.GrantTypes.Password, principal: null);

        await Handler(db).HandleAsync(context);

        context.Principal.Should().BeNull();
    }

    [Fact]
    public async Task Token_exchange_does_not_repopulate_claims()
    {
        await using var db = TestApplicationDbContext.Create();
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, Guid.NewGuid().ToString("D")));
        var principal = new ClaimsPrincipal(identity);
        var context = Context(AuthorizationConstants.GrantTypes.TokenExchange, principal);

        await Handler(db).HandleAsync(context);

        identity.Claims.Should().ContainSingle();
    }

    [Fact]
    public async Task Client_credentials_without_role_adds_no_workload_permissions()
    {
        await using var db = TestApplicationDbContext.Create();
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, "workload-client"));
        var context = Context(OpenIddictConstants.GrantTypes.ClientCredentials, new ClaimsPrincipal(identity));

        await Handler(db).HandleAsync(context);

        identity.FindFirst("principal_type")!.Value.Should().Be("workload");
        identity.FindFirst("permissions").Should().BeNull();
        identity.FindFirst("workload_role_id").Should().BeNull();
    }

    [Fact]
    public async Task Human_principal_gets_profile_roles_and_password_amr_claims()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User
        {
            Id = Guid.NewGuid(), FirstName = "An", LastName = "Nguyen", LicenseNumber = "LIC-42",
            Email = "an@example.test", UserName = "an@example.test"
        };
        var manager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        manager.Setup(x => x.FindByIdAsync(user.Id.ToString("D"))).ReturnsAsync(user);
        manager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Clinician", "Auditor"]);
        manager.Setup(x => x.GetClaimsAsync(user)).ReturnsAsync([]);
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, user.Id.ToString("D")));
        var context = Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity));

        await Handler(db, new DisabledConglomerateTenantRegistry(), manager.Object).HandleAsync(context);

        identity.FindFirst(AuthorizationConstants.Claims.PrincipalType)!.Value.Should().Be(AuthorizationConstants.PrincipalTypes.Human);
        identity.FindFirst("fullName")!.Value.Should().Be("Nguyen An");
        identity.FindFirst("licenseNumber")!.Value.Should().Be("LIC-42");
        identity.FindAll(OpenIddictConstants.Claims.Role).Select(c => c.Value).Should().BeEquivalentTo("Clinician", "Auditor");
        identity.FindFirst("super_admin").Should().BeNull();
        identity.FindFirst("amr")!.Value.Should().Be("pwd");
        identity.FindFirst("scope")!.Value.Should().Be("hishop:permissions");
    }

    [Fact]
    public async Task Human_principal_gets_super_admin_claim_only_when_configured()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), Email = "admin@example.test", UserName = "admin@example.test" };
        var manager = MockManager(user, []);
        manager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Admin"]);
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, user.Id.ToString("D")));
        var context = Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:SuperAdmin:UserIds:0"] = user.Id.ToString("D")
            })
            .Build();

        await Handler(db, new DisabledConglomerateTenantRegistry(), manager.Object, configuration).HandleAsync(context);

        identity.FindFirst("super_admin")!.Value.Should().Be("true");
    }

    [Fact]
    public async Task Conglomerate_human_principal_gets_tenant_id_from_client_binding()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Pilot",
            LastName = "Operator",
            Email = "pilot@example.test",
            UserName = "pilot@example.test"
        };
        var manager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        manager.Setup(x => x.FindByIdAsync(user.Id.ToString("D"))).ReturnsAsync(user);
        manager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Admin"]);
        manager.Setup(x => x.GetClaimsAsync(user)).ReturnsAsync(
        [
            new Claim("tenant_membership", "manufacturing"),
            new Claim("tenant_membership", "nacoms-group")
        ]);
        var registry = new TestConglomerateTenantRegistry(
            enabled: true,
            clientTenants: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["manufacturing-app"] = "manufacturing"
            });
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, user.Id.ToString("D")));
        var context = Context(OpenIddictConstants.GrantTypes.AuthorizationCode, new ClaimsPrincipal(identity));
        context.Transaction!.Request!.ClientId = "manufacturing-app";

        await Handler(db, registry, manager.Object).HandleAsync(context);

        identity.FindFirst("tenant_id")!.Value.Should().Be("manufacturing");
        identity.FindFirst("tenant_memberships")!.Value.Should().Contain("nacoms-group");
    }

    [Fact]
    public async Task Human_principal_prefers_active_primary_facility_and_falls_back_to_legacy_claim()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), FirstName = "Facility", LastName = "User", UserName = "facility@example.test" };
        db.UserFacilities.AddRange(
            new UserFacility { UserId = user.Id, FacilityId = "inactive", IsPrimary = true, IsActive = false },
            new UserFacility { UserId = user.Id, FacilityId = "facility-b", IsPrimary = false, IsActive = true },
            new UserFacility { UserId = user.Id, FacilityId = "facility-a", IsPrimary = true, IsActive = true });
        await db.SaveChangesAsync();
        var manager = MockManager(user, claims: [new Claim("facility_id", "legacy")]);
        var identity = SubjectIdentity(user.Id);

        await Handler(db, new DisabledConglomerateTenantRegistry(), manager.Object)
            .HandleAsync(Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity)));

        identity.FindFirst("facility_id")!.Value.Should().Be("facility-a");
        identity.FindFirst("facility_ids")!.Value.Should().Be("facility-a,facility-b");
    }

    [Fact]
    public async Task Human_principal_preserves_existing_authentication_method_claim()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "mfa@example.test" };
        var manager = MockManager(user, claims: []);
        var identity = SubjectIdentity(user.Id);
        identity.AddClaim(new Claim("amr", "otp"));

        await Handler(db, new DisabledConglomerateTenantRegistry(), manager.Object)
            .HandleAsync(Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity)));

        identity.FindAll("amr").Select(claim => claim.Value).Should().ContainSingle().Which.Should().Be("otp");
    }

    [Fact]
    public async Task Client_credentials_fallback_resolves_role_and_intersects_boundary_permissions()
    {
        await using var db = TestApplicationDbContext.Create();
        var scope = new IamScope { Key = "tenant-a", Kind = "tenant" };
        var role = new IamWorkloadRole
        {
            Key = "legacy-worker",
            Audience = "legacy-client",
            ScopeId = scope.Id,
            PermissionsJson = "[\"patients.view\",\"patients.create\"]"
        };
        db.IamScopes.Add(scope);
        db.IamWorkloadRoles.Add(role);
        db.IamPermissionBoundaries.Add(new IamPermissionBoundary
        {
            PrincipalId = role.Id,
            PrincipalType = AuthorizationConstants.PrincipalTypes.Workload,
            ScopeId = scope.Id,
            AllowedPermissionsJson = "[\"patients.view\"]"
        });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, role.Key));
        var context = Context(OpenIddictConstants.GrantTypes.ClientCredentials, new ClaimsPrincipal(identity));

        await Handler(db).HandleAsync(context);

        identity.FindFirst(AuthorizationConstants.Claims.PrincipalType)!.Value
            .Should().Be(AuthorizationConstants.PrincipalTypes.Workload);
        identity.FindFirst("permissions")!.Value.Should().Be("patients.view");
        identity.FindFirst("workload_role_id")!.Value.Should().Be(role.Id.ToString("D"));
        identity.FindFirst("workload_audience")!.Value.Should().Be("legacy-client");
    }

    [Fact]
    public async Task Human_principal_filters_assigned_sets_break_glass_and_malformed_boundary_json()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "permissions@example.test" };
        var scope = new IamScope { Key = "tenant-a", Kind = "tenant" };
        var published = new IamPermissionSet
        {
            ScopeId = scope.Id,
            LifecycleStatus = AuthorizationConstants.LifecycleStatuses.Published,
            PermissionsJson = "[\"patients.view\",\"patients.create\"]"
        };
        db.IamScopes.Add(scope);
        db.IamPermissionSets.Add(published);
        db.IamPermissionSetAssignments.Add(new IamPermissionSetAssignment
        {
            PermissionSetId = published.Id, PrincipalId = user.Id,
            PrincipalType = AuthorizationConstants.PrincipalTypes.Human,
            ScopeId = scope.Id, Status = AuthorizationConstants.LifecycleStatuses.Active
        });
        db.BreakGlassRequests.Add(new BreakGlassRequest
        {
            Id = Guid.NewGuid(), SubjectUserId = user.Id, PermissionCode = "patients.view",
            Status = "approved", ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        db.IamPermissionBoundaries.Add(new IamPermissionBoundary
        {
            PrincipalId = user.Id,
            PrincipalType = AuthorizationConstants.PrincipalTypes.Human,
            ScopeId = scope.Id,
            AllowedPermissionsJson = "not-json",
            ResourceConstraintsJson = "{\"facility\":\"A\"}"
        });
        db.IamPermissionBoundaries.Add(new IamPermissionBoundary
        {
            PrincipalId = user.Id,
            PrincipalType = AuthorizationConstants.PrincipalTypes.Human,
            ScopeId = scope.Id,
            AllowedPermissionsJson = "[\"patients.view\"]",
            ResourceConstraintsJson = "{\"facility\":\"B\"}"
        });
        db.IamServiceDefinitions.Add(new IamServiceDefinition { Key = "patients", PermissionPrefix = "patients" });
        await db.SaveChangesAsync();
        var manager = MockManager(user, claims: []);
        var identity = SubjectIdentity(user.Id);

        await Handler(db, new DisabledConglomerateTenantRegistry(), manager.Object)
            .HandleAsync(Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity)));

        identity.FindFirst("permissions").Should().BeNull();
        identity.FindFirst("authorization_constraints")!.Value.Should().Contain("facility");
    }

    [Fact]
    public async Task Human_principal_ignores_expired_and_malformed_assigned_permission_sets()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "assigned@example.test" };
        var expiredSet = new IamPermissionSet
        {
            LifecycleStatus = AuthorizationConstants.LifecycleStatuses.Published,
            PermissionsJson = "[\"patients.view\"]"
        };
        db.IamPermissionSets.Add(expiredSet);
        db.IamPermissionSetAssignments.Add(new IamPermissionSetAssignment
        {
            PermissionSetId = expiredSet.Id, PrincipalId = user.Id,
            PrincipalType = AuthorizationConstants.PrincipalTypes.Human,
            Status = AuthorizationConstants.LifecycleStatuses.Active,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });
        db.IamServiceDefinitions.Add(new IamServiceDefinition { Key = "patients", PermissionPrefix = "patients" });
        await db.SaveChangesAsync();
        var manager = MockManager(user, claims: []);
        var identity = SubjectIdentity(user.Id);

        await Handler(db, new DisabledConglomerateTenantRegistry(), manager.Object)
            .HandleAsync(Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity)));

        identity.FindFirst("permissions").Should().BeNull();
    }

    [Fact]
    public async Task Enabled_registry_adds_membership_claims_for_non_conglomerate_client()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "tenant@example.test" };
        var manager = MockManager(user,
            claims: [new Claim("tenant_membership", "tenant-a"), new Claim("tenant_membership", "tenant-b")]);
        var registry = new TestConglomerateTenantRegistry(enabled: true, clientTenants: []);
        var identity = SubjectIdentity(user.Id);
        var context = Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity));
        context.Transaction!.Request!.ClientId = "public-client";

        await Handler(db, registry, manager.Object).HandleAsync(context);

        context.Error.Should().BeNull();
        identity.FindFirst("tenant_id")!.Value.Should().Be("tenant-a");
        identity.FindAll("tenant_membership").Select(claim => claim.Value).Should().BeEquivalentTo("tenant-a", "tenant-b");
        identity.FindFirst("tenant_memberships")!.Value.Should().Be("tenant-a,tenant-b");
    }

    [Fact]
    public async Task Conglomerate_client_issues_membership_claim_for_its_bound_tenant_only()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "group-admin@example.test" };
        var manager = MockManager(user,
            claims: [new Claim("tenant_membership", "nacoms-group"), new Claim("tenant_membership", "customer-acme")]);
        var registry = new TestConglomerateTenantRegistry(
            enabled: true,
            clientTenants: new Dictionary<string, string> { ["group-hq-admin"] = "nacoms-group" });
        var identity = SubjectIdentity(user.Id);
        var context = Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity));
        context.Transaction!.Request!.ClientId = "group-hq-admin";

        await Handler(db, registry, manager.Object).HandleAsync(context);

        context.Error.Should().BeNull();
        identity.FindFirst("tenant_id")!.Value.Should().Be("nacoms-group");
        identity.FindAll("tenant_membership").Select(claim => claim.Value)
            .Should().ContainSingle().Which.Should().Be("nacoms-group");
    }

    [Fact]
    public async Task Regular_client_issues_verified_user_membership_when_conglomerate_mode_is_disabled()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "group-admin@example.test" };
        var manager = MockManager(user, claims: [new Claim("tenant_membership", "nacoms-group")]);
        var identity = SubjectIdentity(user.Id);
        var context = Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity));
        context.Transaction!.Request!.ClientId = "identity-admin";

        await Handler(db, new DisabledConglomerateTenantRegistry(), manager.Object).HandleAsync(context);

        context.Error.Should().BeNull();
        identity.FindFirst("tenant_id")!.Value.Should().Be("nacoms-group");
        identity.FindAll("tenant_membership").Select(claim => claim.Value)
            .Should().ContainSingle().Which.Should().Be("nacoms-group");
    }

    [Fact]
    public async Task Conglomerate_client_without_binding_rejects_human_token()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "unbound@example.test" };
        var manager = MockManager(user, claims: [new Claim("tenant_membership", "tenant-a")]);
        var registry = new TestConglomerateTenantRegistry(
            enabled: true,
            clientTenants: new Dictionary<string, string> { ["unbound-client"] = string.Empty });
        var identity = SubjectIdentity(user.Id);
        var context = Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity));
        context.Transaction!.Request!.ClientId = "unbound-client";

        await Handler(db, registry, manager.Object).HandleAsync(context);

        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidClient);
        identity.FindFirst("tenant_id").Should().BeNull();
    }

    [Fact]
    public async Task Conglomerate_client_rejects_user_without_required_tenant_membership()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), UserName = "wrong-tenant@example.test" };
        var manager = MockManager(user, claims: [new Claim("tenant_membership", "tenant-b")]);
        var registry = new TestConglomerateTenantRegistry(
            enabled: true,
            clientTenants: new Dictionary<string, string> { ["tenant-client"] = "tenant-a" });
        var identity = SubjectIdentity(user.Id);
        var context = Context(OpenIddictConstants.GrantTypes.Password, new ClaimsPrincipal(identity));
        context.Transaction!.Request!.ClientId = "tenant-client";

        await Handler(db, registry, manager.Object).HandleAsync(context);

        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidGrant);
        context.ErrorDescription.Should().Contain("not authorized for this tenant");
    }

    [Fact]
    public async Task Openship_console_client_receives_active_prefixed_groups_in_access_and_identity_tokens()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), Email = "ops@example.test", UserName = "ops@example.test" };
        var readGroup = new IamGroup { Key = "openship-console-read", DisplayName = "Console read" };
        var adminGroup = new IamGroup { Key = "openship-console-admin", DisplayName = "Console admin" };
        var inactiveGroup = new IamGroup { Key = "openship-console-retired", DisplayName = "Retired", IsActive = false };
        var otherGroup = new IamGroup { Key = "finance", DisplayName = "Finance" };
        db.IamGroups.AddRange(readGroup, adminGroup, inactiveGroup, otherGroup);
        db.IamGroupMemberships.AddRange(
            new IamGroupMembership { GroupId = readGroup.Id, UserId = user.Id },
            new IamGroupMembership { GroupId = adminGroup.Id, UserId = user.Id },
            new IamGroupMembership { GroupId = inactiveGroup.Id, UserId = user.Id },
            new IamGroupMembership { GroupId = otherGroup.Id, UserId = user.Id });
        await db.SaveChangesAsync();
        var identity = SubjectIdentity(user.Id);
        // A groups claim carried over from the code/refresh token must not survive.
        identity.AddClaim(new Claim("groups", "openship-console-stale"));

        await Handler(db, new DisabledConglomerateTenantRegistry(), MockManager(user, []).Object)
            .HandleAsync(Context(OpenIddictConstants.GrantTypes.AuthorizationCode, new ClaimsPrincipal(identity), "openship-grafana"));

        var groups = identity.FindAll("groups").Should().ContainSingle().Subject;
        groups.ValueType.Should().Be("JSON_ARRAY");
        System.Text.Json.JsonSerializer.Deserialize<string[]>(groups.Value)
            .Should().BeEquivalentTo("openship-console-admin", "openship-console-read");
        groups.GetDestinations().Should().Contain(OpenIddictConstants.Destinations.AccessToken)
            .And.Contain(OpenIddictConstants.Destinations.IdentityToken);
    }

    [Fact]
    public async Task Single_group_is_still_a_json_array_for_strict_relying_parties()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), Email = "ops@example.test", UserName = "ops@example.test" };
        var group = new IamGroup { Key = "openship-ops-engineer", DisplayName = "Ops" };
        db.IamGroups.Add(group);
        db.IamGroupMemberships.Add(new IamGroupMembership { GroupId = group.Id, UserId = user.Id });
        await db.SaveChangesAsync();
        var identity = SubjectIdentity(user.Id);

        await Handler(db, new DisabledConglomerateTenantRegistry(), MockManager(user, []).Object)
            .HandleAsync(Context(OpenIddictConstants.GrantTypes.AuthorizationCode, new ClaimsPrincipal(identity), "openship-warpgate"));

        var groups = identity.FindAll("groups").Should().ContainSingle().Subject;
        groups.ValueType.Should().Be("JSON_ARRAY");
        groups.Value.Should().Be("[\"openship-ops-engineer\"]");
    }

    [Fact]
    public void Json_array_claims_serialize_as_arrays_in_the_jwt_payload()
    {
        // OpenIddict hands the filtered identity to JsonWebTokenHandler; prove the
        // JSON_ARRAY value type ends up as a real array (OIDC Core: amr is an array).
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("sub", "u-1"));
        identity.AddClaim(new Claim("amr", "[\"otp\"]", CustomPopulateTokenClaims.JsonArrayClaimValueType));
        identity.AddClaim(new Claim("groups", "[\"openship-ops-engineer\"]", CustomPopulateTokenClaims.JsonArrayClaimValueType));
        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var jwt = handler.CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor { Subject = identity });

        var payload = System.Text.Json.JsonDocument.Parse(
            Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Decode(jwt.Split('.')[1])).RootElement;

        payload.GetProperty("amr").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        payload.GetProperty("amr")[0].GetString().Should().Be("otp");
        payload.GetProperty("groups").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public void Amr_becomes_an_array_in_the_identity_token_and_stays_scalar_in_the_access_token()
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("amr", "pwd"));
        identity.AddClaim(new Claim("amr", "otp"));

        CustomPopulateTokenClaims.NormalizeAmrForStrictRelyingParties(identity);
        // Idempotent: a second pass (refresh) must not duplicate the array claim.
        CustomPopulateTokenClaims.NormalizeAmrForStrictRelyingParties(identity);

        var claims = identity.FindAll("amr").ToArray();
        var scalars = claims.Where(claim => claim.ValueType != "JSON_ARRAY").ToArray();
        scalars.Should().HaveCount(2);
        foreach (var scalar in scalars)
            scalar.GetDestinations().Should().Equal(OpenIddictConstants.Destinations.AccessToken);
        var array = claims.Should().ContainSingle(claim => claim.ValueType == "JSON_ARRAY").Subject;
        array.Value.Should().Be("[\"pwd\",\"otp\"]");
        array.GetDestinations().Should().Equal(OpenIddictConstants.Destinations.IdentityToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("admin-app")]
    public async Task Groups_claim_is_not_issued_to_clients_outside_the_openship_console_list(string? clientId)
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), Email = "ops@example.test", UserName = "ops@example.test" };
        var group = new IamGroup { Key = "openship-console-read", DisplayName = "Console read" };
        db.IamGroups.Add(group);
        db.IamGroupMemberships.Add(new IamGroupMembership { GroupId = group.Id, UserId = user.Id });
        await db.SaveChangesAsync();
        var identity = SubjectIdentity(user.Id);
        identity.AddClaim(new Claim("groups", "openship-console-read"));

        await Handler(db, new DisabledConglomerateTenantRegistry(), MockManager(user, []).Object)
            .HandleAsync(Context(OpenIddictConstants.GrantTypes.RefreshToken, new ClaimsPrincipal(identity), clientId));

        identity.FindAll("groups").Should().BeEmpty();
    }

    [Fact]
    public async Task Kubectl_client_gets_a_short_access_token_and_other_console_clients_keep_the_default()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), Email = "ops@example.test", UserName = "ops@example.test" };
        var serverOptions = new OpenIddictServerOptions { AccessTokenLifetime = TimeSpan.FromHours(1) };

        var kubectl = new ClaimsPrincipal(SubjectIdentity(user.Id));
        await Handler(db, new DisabledConglomerateTenantRegistry(), MockManager(user, []).Object)
            .HandleAsync(Context(OpenIddictConstants.GrantTypes.AuthorizationCode, kubectl, "openship-kubectl", serverOptions));
        kubectl.GetAccessTokenLifetime().Should().Be(TimeSpan.FromMinutes(10));

        var grafana = new ClaimsPrincipal(SubjectIdentity(user.Id));
        await Handler(db, new DisabledConglomerateTenantRegistry(), MockManager(user, []).Object)
            .HandleAsync(Context(OpenIddictConstants.GrantTypes.AuthorizationCode, grafana, "openship-grafana", serverOptions));
        grafana.GetAccessTokenLifetime().Should().BeNull();
    }

    [Fact]
    public async Task Break_glass_elevation_adds_the_group_and_caps_tokens_at_the_grant_expiry()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), Email = "ops@example.test", UserName = "ops@example.test" };
        db.BreakGlassRequests.Add(new BreakGlassRequest
        {
            Id = Guid.NewGuid(),
            SubjectUserId = user.Id,
            PermissionCode = HisHopePermissions.OpenShip.NodeRoot,
            FacilityId = "facility-a",
            Reason = "Node incident",
            Status = "approved",
            RequestedBy = Guid.NewGuid().ToString(),
            RequestedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(7)
        });
        await db.SaveChangesAsync();
        var serverOptions = new OpenIddictServerOptions
        {
            AccessTokenLifetime = TimeSpan.FromHours(1),
            IdentityTokenLifetime = TimeSpan.FromMinutes(20)
        };
        var principal = new ClaimsPrincipal(SubjectIdentity(user.Id));

        await Handler(db, new DisabledConglomerateTenantRegistry(), MockManager(user, []).Object)
            .HandleAsync(Context(OpenIddictConstants.GrantTypes.RefreshToken, principal, "openship-warpgate", serverOptions));

        principal.FindAll("groups").Should().ContainSingle().Which.Value.Should().Contain("\"openship-breakglass-node-admin\"");
        principal.GetAccessTokenLifetime().Should().BeCloseTo(TimeSpan.FromMinutes(7), TimeSpan.FromSeconds(30));
        principal.GetIdentityTokenLifetime().Should().BeCloseTo(TimeSpan.FromMinutes(7), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Inactive_user_is_rejected_at_the_token_endpoint()
    {
        await using var db = TestApplicationDbContext.Create();
        var user = new User { Id = Guid.NewGuid(), Email = "leaver@example.test", UserName = "leaver@example.test", IsActive = false };
        var identity = SubjectIdentity(user.Id);
        var context = Context(OpenIddictConstants.GrantTypes.RefreshToken, new ClaimsPrincipal(identity), "openship-headscale");

        await Handler(db, new DisabledConglomerateTenantRegistry(), MockManager(user, []).Object).HandleAsync(context);

        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidGrant);
        context.ErrorDescription.Should().Be("The user account is disabled.");
        identity.FindFirst(AuthorizationConstants.Claims.PrincipalType).Should().BeNull();
    }

    private sealed class TestConglomerateTenantRegistry : IConglomerateTenantRegistry
    {
        private readonly Dictionary<string, string> _clientTenants;

        public TestConglomerateTenantRegistry(bool enabled, Dictionary<string, string> clientTenants)
        {
            IsEnabled = enabled;
            _clientTenants = clientTenants;
        }

        public bool IsEnabled { get; }

        public string HqCustomerVisibility => ConglomerateConstants.HqCustomerVisibilityNone;

        public bool IsConglomerateClient(string? clientId) =>
            !string.IsNullOrWhiteSpace(clientId) && _clientTenants.ContainsKey(clientId);

        public string? GetClientTenant(string? clientId) =>
            clientId is not null && _clientTenants.TryGetValue(clientId, out var tenant) ? tenant : null;

        public string GetPortalClass(string? clientId) => ConglomerateConstants.PortalClassOperator;

        public string GetTenantClass(string tenantKey) => ConglomerateConstants.TenantClassInternal;

        public string? GetOperatorHome(string tenantKey) => null;

        public IReadOnlyList<string> GetClientIdsForTenant(string tenantKey) =>
            _clientTenants
                .Where(pair => string.Equals(pair.Value, tenantKey, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .ToList();

        public IReadOnlyList<string> GetCustomerTenantsForOperator(string operatorTenantKey) => [];

        public bool IsCustomerTenant(string tenantKey) => false;

        public ConglomerateTenantOptions? GetTenantProfile(string tenantKey) => null;

        public IReadOnlyList<CrossTenantAllowedPairOptions> AllowedCrossTenantPairs { get; } = [];
    }

    private static CustomPopulateTokenClaims Handler(IApplicationDbContext db) =>
        Handler(db, new DisabledConglomerateTenantRegistry());

    private static CustomPopulateTokenClaims Handler(
        IApplicationDbContext db,
        IConglomerateTenantRegistry tenantRegistry,
        UserManager<User>? userManager = null,
        IConfiguration? configuration = null)
    {
        var consoleOptions = new OpenShipConsoleOptions();
        consoleOptions.ApplyDefaults();
        var options = Options.Create(consoleOptions);
        return new(userManager ?? CreateUserManager(), db, tenantRegistry, configuration ?? new ConfigurationBuilder().Build(),
            NullLogger<CustomPopulateTokenClaims>.Instance,
            new OpenShipConsoleAccessResolver(db, options, TimeProvider.System), options);
    }

    private static Mock<UserManager<User>> MockManager(User user, IEnumerable<Claim> claims)
    {
        var manager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        manager.Setup(x => x.FindByIdAsync(user.Id.ToString("D"))).ReturnsAsync(user);
        manager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync([]);
        manager.Setup(x => x.GetClaimsAsync(user)).ReturnsAsync(claims.ToList());
        return manager;
    }

    private static ClaimsIdentity SubjectIdentity(Guid userId)
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, userId.ToString("D")));
        return identity;
    }

    private static OpenIddictServerEvents.HandleTokenRequestContext Context(
        string grantType,
        ClaimsPrincipal? principal,
        string? clientId = null,
        OpenIddictServerOptions? options = null)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { GrantType = grantType, ClientId = clientId }
        };
        if (options is not null)
            transaction.Options = options;
        var context = new OpenIddictServerEvents.HandleTokenRequestContext(transaction);
        context.Principal = principal;
        return context;
    }

    private static UserManager<User> CreateUserManager()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        return new UserManager<User>(
            new UserStoreStub(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services,
            NullLogger<UserManager<User>>.Instance);
    }

    private sealed class UserStoreStub : IUserStore<User>
    {
        public Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public void Dispose() { }
        public Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
        public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.NormalizedUserName);
        public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString("D"));
        public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName);
        public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    }
}
