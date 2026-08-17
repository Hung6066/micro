using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.OpenIddict;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.SharedKernel.Authorization;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class OpenIddictWorkloadHandlerTests
{
    [Fact]
    public async Task Client_credentials_handler_ignores_other_grants()
    {
        await using var db = TestApplicationDbContext.Create();
        var context = Context("password", clientId: "client");
        var sessions = new Mock<IWorkloadSessionStore>();

        await new CustomHandleClientCredentialsRequest(db, sessions.Object).HandleAsync(context);

        context.IsRejected.Should().BeFalse();
        context.Principal.Should().BeNull();
        sessions.Verify(x => x.RegisterAsync(It.IsAny<WorkloadSessionRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Client_credentials_handler_rejects_missing_client_id()
    {
        await using var db = TestApplicationDbContext.Create();
        var context = Context(OpenIddictConstants.GrantTypes.ClientCredentials, clientId: null);

        await new CustomHandleClientCredentialsRequest(db, Mock.Of<IWorkloadSessionStore>()).HandleAsync(context);

        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidClient);
    }

    [Fact]
    public async Task Client_credentials_handler_rejects_unknown_workload()
    {
        await using var db = TestApplicationDbContext.Create();
        var context = Context(OpenIddictConstants.GrantTypes.ClientCredentials, clientId: "unknown");

        await new CustomHandleClientCredentialsRequest(db, Mock.Of<IWorkloadSessionStore>()).HandleAsync(context);

        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.UnauthorizedClient);
    }

    [Fact]
    public async Task Client_credentials_resolves_role_permissions_boundary_tenant_and_published_policy()
    {
        await using var db = TestApplicationDbContext.Create();
        var scope = new IamScope { Key = "tenant-a", DisplayName = "Tenant A", Kind = "tenant" };
        var role = new IamWorkloadRole
        {
            Key = "billing-worker", Audience = "billing-client", ScopeId = scope.Id,
            TrustPolicyJson = "{\"principals\":[\"billing-client\"]}",
            PermissionsJson = "[\"patients.view\",\"invalid.permission\"]", MaxSessionSeconds = 1
        };
        var permissionSet = new IamPermissionSet
        {
            ScopeId = scope.Id, LifecycleStatus = AuthorizationConstants.LifecycleStatuses.Published,
            PermissionsJson = "[\"patients.create\",\"patients.view\"]"
        };
        var boundary = new IamPermissionBoundary
        {
            PrincipalId = role.Id, PrincipalType = AuthorizationConstants.PrincipalTypes.Workload,
            ScopeId = scope.Id, AllowedPermissionsJson = "[\"patients.view\"]", ResourceConstraintsJson = "{\"facility\":\"A\"}"
        };
        db.IamScopes.Add(scope);
        db.IamWorkloadRoles.Add(role);
        db.IamPermissionSets.Add(permissionSet);
        db.IamPermissionSetAssignments.Add(new IamPermissionSetAssignment
        {
            PermissionSetId = permissionSet.Id, PrincipalId = role.Id,
            PrincipalType = AuthorizationConstants.PrincipalTypes.Workload, ScopeId = scope.Id
        });
        db.IamPermissionBoundaries.Add(boundary);
        db.IamResourcePolicies.Add(new IamResourcePolicy
        {
            ScopeId = scope.Id, ServiceKey = "billing", ResourcePattern = "invoice/*",
            LifecycleStatus = AuthorizationConstants.LifecycleStatuses.Published,
            StatementsJson = "[{\"principal\":\"billing-worker\",\"actions\":[\"read\"],\"effect\":\"ALLOW\"}]"
        });
        await db.SaveChangesAsync();
        var sessions = new Mock<IWorkloadSessionStore>();
        var context = Context(OpenIddictConstants.GrantTypes.ClientCredentials, "billing-client");
        context.Request.SetParameter("scope", "api billing.read");

        await new CustomHandleClientCredentialsRequest(db, sessions.Object).HandleAsync(context);

        context.IsRejected.Should().BeFalse();
        context.Principal.Should().NotBeNull();
        context.Principal!.FindFirst("permissions")!.Value.Should().Be("patients.view");
        context.Principal.FindFirst("tenant_id")!.Value.Should().Be("tenant-a");
        context.Principal.FindFirst("authorization_constraints")!.Value.Should().Contain("facility");
        context.Principal.FindFirst("resource_policies")!.Value.Should().Contain("billing");
        sessions.Verify(x => x.RegisterAsync(It.Is<WorkloadSessionRecord>(s => s.ClientId == "billing-client"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Client_credentials_rejects_untrusted_active_role()
    {
        await using var db = TestApplicationDbContext.Create();
        db.IamWorkloadRoles.Add(new IamWorkloadRole
        {
            Key = "worker", Audience = "client", TrustPolicyJson = "{\"principals\":[\"other\"]}", PermissionsJson = "[]"
        });
        await db.SaveChangesAsync();
        var context = Context(OpenIddictConstants.GrantTypes.ClientCredentials, "client");

        await new CustomHandleClientCredentialsRequest(db, Mock.Of<IWorkloadSessionStore>()).HandleAsync(context);

        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.UnauthorizedClient);
    }

    [Fact]
    public void Workload_policy_intersection_is_deterministic_and_deduplicated()
    {
        WorkloadTokenExchangePolicy.IntersectPermissions(
            ["user.read", "user.read", "", "user.write"],
            new HashSet<string>(["user.write", "missing"], StringComparer.Ordinal))
            .Should().Equal("user.write");
        WorkloadTokenExchangePolicy.TrustsActor("{\"principals\":[\"actor\"]}", "actor").Should().BeTrue();
        WorkloadTokenExchangePolicy.TrustsActor("{\"principals\":[]}", "actor").Should().BeFalse();
        WorkloadTokenExchangePolicy.TrustsActor("not-json", "actor").Should().BeFalse();
    }

    [Fact]
    public async Task Token_exchange_rejects_missing_required_parameters()
    {
        await using var db = TestApplicationDbContext.Create();
        var context = Context(AuthorizationConstants.GrantTypes.TokenExchange, clientId: "actor");
        var handler = new CustomHandleTokenExchangeRequest(
            db,
            OptionsMonitor(new OpenIddictServerOptions()),
            Mock.Of<IWorkloadSessionStore>());

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidRequest);
    }

    [Fact]
    public async Task Token_exchange_rejects_invalid_subject_token()
    {
        await using var db = TestApplicationDbContext.Create();
        var context = Context(AuthorizationConstants.GrantTypes.TokenExchange, clientId: "actor");
        context.Request.SetParameter("subject_token", "not-a-jwt");
        context.Request.SetParameter("subject_token_type", "urn:ietf:params:oauth:token-type:access_token");
        context.Request.SetParameter("requested_role", "role");
        context.Request.SetParameter("audience", "service");
        var handler = new CustomHandleTokenExchangeRequest(
            db,
            OptionsMonitor(new OpenIddictServerOptions()),
            Mock.Of<IWorkloadSessionStore>());

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidGrant);
    }

    [Fact]
    public async Task Token_exchange_validates_subject_intersects_permissions_and_registers_session()
    {
        await using var db = TestApplicationDbContext.Create();
        var scope = new IamScope { Key = "tenant-b", Kind = "tenant" };
        var role = new IamWorkloadRole
        {
            Key = "target-worker", Audience = "target-service", ScopeId = scope.Id,
            TrustPolicyJson = "{\"principals\":[\"actor-client\"]}",
            PermissionsJson = "[\"patients.view\",\"patients.create\"]"
        };
        db.IamScopes.Add(scope);
        db.IamWorkloadRoles.Add(role);
        await db.SaveChangesAsync();
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("01234567890123456789012345678901"));
        var previousMapInboundClaims = JwtSecurityTokenHandler.DefaultMapInboundClaims;
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "test", audience: "test",
            claims: [new Claim(OpenIddictConstants.Claims.Subject, "source-user")],
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
        var options = new OpenIddictServerOptions();
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.IssuerSigningKey = key;
        options.TokenValidationParameters.ValidateIssuer = false;
        options.TokenValidationParameters.ValidateAudience = false;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
        options.TokenValidationParameters.TypeValidator = (_, _, _) => null;
        var context = Context(AuthorizationConstants.GrantTypes.TokenExchange, "actor-client");
        context.Request.SetParameter("subject_token", token);
        context.Request.SetParameter("subject_token_type", "urn:ietf:params:oauth:token-type:access_token");
        context.Request.SetParameter("requested_role", "target-worker");
        context.Request.SetParameter("audience", "target-service");
        context.Request.SetParameter("requested_permissions", "patients.view,patients.delete");
        context.Request.SetParameter("scope", "api sensitive.read");
        var sessions = new Mock<IWorkloadSessionStore>();

        try
        {
            await new CustomHandleTokenExchangeRequest(db, OptionsMonitor(options), sessions.Object).HandleAsync(context);
        }
        finally
        {
            JwtSecurityTokenHandler.DefaultMapInboundClaims = previousMapInboundClaims;
        }

        context.IsRejected.Should().BeFalse(context.ErrorDescription);
        context.Principal.Should().NotBeNull();
        context.Principal!.FindFirst(OpenIddictConstants.Claims.Subject)!.Value.Should().Be("source-user");
        context.Principal.FindFirst("permissions")!.Value.Should().Be("patients.view");
        context.Principal.GetScopes().Should().ContainSingle("api");
        context.Principal.FindFirst("act")!.Value.Should().Be("actor-client");
        sessions.Verify(x => x.RegisterAsync(It.Is<WorkloadSessionRecord>(s => s.ClientId == "actor-client"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Token_exchange_ignores_non_exchange_grants()
    {
        await using var db = TestApplicationDbContext.Create();
        var context = Context(OpenIddictConstants.GrantTypes.ClientCredentials, clientId: "actor");
        var handler = new CustomHandleTokenExchangeRequest(
            db,
            OptionsMonitor(new OpenIddictServerOptions()),
            Mock.Of<IWorkloadSessionStore>());

        await handler.HandleAsync(context);

        context.IsRejected.Should().BeFalse();
        context.Principal.Should().BeNull();
    }

    private static OpenIddictServerEvents.HandleTokenRequestContext Context(string grantType, string? clientId)
    {
        var request = new OpenIddictRequest { GrantType = grantType, ClientId = clientId };
        var transaction = new OpenIddictServerTransaction { Request = request };
        return new OpenIddictServerEvents.HandleTokenRequestContext(transaction);
    }

    private static IOptionsMonitor<OpenIddictServerOptions> OptionsMonitor(OpenIddictServerOptions value)
    {
        var monitor = new Mock<IOptionsMonitor<OpenIddictServerOptions>>();
        monitor.Setup(x => x.CurrentValue).Returns(value);
        monitor.Setup(x => x.Get(It.IsAny<string>())).Returns(value);
        return monitor.Object;
    }
}
