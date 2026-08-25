using FluentAssertions;
using Grpc.Core;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class GrpcIdentityServiceTests
{
    [Fact]
    public async Task IntrospectToken_with_empty_token_is_inactive()
    {
        await using var db = CreateDb();
        var service = CreateService(db, CreateUserManager(), CreateRedis(false));

        dynamic response = await InvokeAsync(service, "IntrospectToken", "IntrospectRequest");

        ((bool)response.Active).Should().BeFalse();
    }

    [Fact]
    public async Task IntrospectToken_with_malformed_token_is_inactive()
    {
        await using var db = CreateDb();
        var service = CreateService(db, CreateUserManager(), CreateRedis(false));

        dynamic response = await InvokeAsync(service, "IntrospectToken", "IntrospectRequest", request =>
            Set(request, "Token", "not-a-jwt"));

        ((bool)response.Active).Should().BeFalse();
    }

    [Fact]
    public async Task IntrospectToken_with_valid_claims_returns_claims_when_user_is_unknown()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager();
        userManager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        var service = CreateService(db, userManager, CreateRedis(false));
        var userId = Guid.NewGuid();
        var token = JwtWithClaims($"{{\"sub\":\"{userId}\",\"client_id\":\"client\",\"exp\":\"123\",\"iat\":\"100\",\"scope\":\"openid\",\"unique_name\":\"alice\",\"amr\":\"mfa\",\"jti\":\"jti-1\"}}");

        dynamic response = await InvokeAsync(service, "IntrospectToken", "IntrospectRequest", request =>
            Set(request, "Token", token));

        ((bool)response.Active).Should().BeTrue();
        ((string)response.Sub).Should().Be(userId.ToString());
        ((string)response.ClientId).Should().Be("client");
        ((long)response.Exp).Should().Be(123);
        ((long)response.Iat).Should().Be(100);
        ((string)response.Scope).Should().Be("openid");
        ((string)response.Username).Should().Be("alice");
        ((IEnumerable<string>)response.Amr).Should().ContainSingle().Which.Should().Be("mfa");
        ((string)response.Jti).Should().Be("jti-1");
    }

    [Fact]
    public async Task IntrospectToken_blacklisted_jti_is_inactive()
    {
        await using var db = CreateDb();
        var service = CreateService(db, CreateUserManager(), CreateRedis(true));
        var token = JwtWithClaims("{\"sub\":\"not-a-guid\",\"jti\":\"revoked\"}");

        dynamic response = await InvokeAsync(service, "IntrospectToken", "IntrospectRequest", request =>
            Set(request, "Token", token));

        ((bool)response.Active).Should().BeFalse();
    }

    [Fact]
    public async Task GetUser_unknown_user_returns_not_found_rpc_status()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager();
        userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((User?)null);
        var service = CreateService(db, userManager, CreateRedis(false));

        var exception = await FluentActions.Invoking(() => InvokeAsync(service, "GetUser", "GetUserRequest", request =>
            Set(request, "UserId", "missing"))).Should().ThrowAsync<RpcException>();
        exception.Which.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task CheckPermission_rejects_an_invalid_user_id_without_querying_database()
    {
        await using var db = CreateDb();
        var service = CreateService(db, CreateUserManager(), CreateRedis(false));

        dynamic response = await InvokeAsync(service, "CheckPermission", "CheckPermissionRequest", request =>
        {
            Set(request, "UserId", "invalid");
            Set(request, "PermissionCode", "users.read");
        });

        ((bool)response.HasPermission).Should().BeFalse();
    }

    [Fact]
    public async Task CheckPermission_returns_true_for_a_role_permission()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
        db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionCode = "users.read" });
        await db.SaveChangesAsync();
        var service = CreateService(db, CreateUserManager(), CreateRedis(false));

        dynamic response = await InvokeAsync(service, "CheckPermission", "CheckPermissionRequest", request =>
        {
            Set(request, "UserId", userId.ToString());
            Set(request, "PermissionCode", "USERS.READ");
        });

        ((bool)response.HasPermission).Should().BeTrue();
    }

    [Fact]
    public async Task CheckAnyPermission_rejects_an_invalid_user_id_without_querying_database()
    {
        await using var db = CreateDb();
        var service = CreateService(db, CreateUserManager(), CreateRedis(false));

        dynamic response = await InvokeAsync(service, "CheckAnyPermission", "CheckAnyPermissionRequest", request =>
        {
            Set(request, "UserId", "invalid");
            ((System.Collections.IList)request.GetType().GetProperty("PermissionCodes")!.GetValue(request)!).Add("users.read");
        });

        ((bool)response.HasAny).Should().BeFalse();
    }

    [Fact]
    public async Task CheckAnyPermission_returns_true_when_any_requested_permission_is_granted()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
        db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionCode = "users.read" });
        await db.SaveChangesAsync();
        var service = CreateService(db, CreateUserManager(), CreateRedis(false));

        dynamic response = await InvokeAsync(service, "CheckAnyPermission", "CheckAnyPermissionRequest", request =>
        {
            Set(request, "UserId", userId.ToString());
            var codes = (System.Collections.IList)request.GetType().GetProperty("PermissionCodes")!.GetValue(request)!;
            codes.Add("admin.write");
            codes.Add("USERS.READ");
        });

        ((bool)response.HasAny).Should().BeTrue();
    }

    [Fact]
    public async Task GetUser_returns_identity_and_permissions()
    {
        await using var db = CreateDb();
        var user = new User
        {
            UserName = "alice",
            Email = "alice@example.test",
            FirstName = "Alice",
            LastName = "Example",
            TwoFactorEnabled = true
        };
        var roleId = Guid.NewGuid();
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = roleId });
        db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionCode = "users.read" });
        await db.SaveChangesAsync();
        var userManager = CreateUserManager();
        userManager.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Reader"]);
        var service = CreateService(db, userManager, CreateRedis(false));

        dynamic response = await InvokeAsync(service, "GetUser", "GetUserRequest", request =>
            Set(request, "UserId", user.Id.ToString()));

        ((string)response.UserId).Should().Be(user.Id.ToString());
        ((string)response.Username).Should().Be("alice");
        ((string)response.Email).Should().Be("alice@example.test");
        ((string)response.FullName).Should().Be("Example Alice");
        ((bool)response.MfaEnabled).Should().BeTrue();
        ((IEnumerable<string>)response.Roles).Should().ContainSingle().Which.Should().Be("Reader");
        ((IEnumerable<string>)response.Permissions).Should().ContainSingle().Which.Should().Be("users.read");
    }

    [Fact]
    public async Task GetUserRoles_unknown_user_returns_empty_roles()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager();
        userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((User?)null);
        var service = CreateService(db, userManager, CreateRedis(false));

        dynamic response = await InvokeAsync(service, "GetUserRoles", "GetUserRolesRequest", request =>
            Set(request, "UserId", "missing"));

        ((IEnumerable<string>)response.Roles).Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRoles_returns_roles_for_an_existing_user()
    {
        await using var db = CreateDb();
        var user = new User { UserName = "alice" };
        var userManager = CreateUserManager();
        userManager.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        userManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Reader", "Auditor"]);
        var service = CreateService(db, userManager, CreateRedis(false));

        dynamic response = await InvokeAsync(service, "GetUserRoles", "GetUserRolesRequest", request =>
            Set(request, "UserId", user.Id.ToString()));

        ((IEnumerable<string>)response.Roles).Should().BeEquivalentTo("Reader", "Auditor");
    }

    [Fact]
    public async Task RevokeUserTokens_updates_security_stamp_for_an_existing_user()
    {
        await using var db = CreateDb();
        var user = new User { UserName = "alice" };
        var userManager = CreateUserManager();
        userManager.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        userManager.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);
        var service = CreateService(db, userManager, CreateRedis(false));

        dynamic response = await InvokeAsync(service, "RevokeUserTokens", "RevokeUserTokensRequest", request =>
        {
            Set(request, "UserId", user.Id.ToString());
            Set(request, "Reason", "security-test");
        });

        ((int)response.TokensRevoked).Should().Be(1);
        userManager.Verify(x => x.UpdateSecurityStampAsync(user), Times.Once);
    }

    [Fact]
    public async Task CheckPermission_combines_role_group_permission_set_and_break_glass_grants()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var permissionSetId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
        db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionCode = "users.read" });
        db.IamGroupMemberships.Add(new IamGroupMembership { UserId = userId, GroupId = groupId });
        db.IamServiceDefinitions.Add(new IamServiceDefinition { PermissionPrefix = "users" });
        db.IamPermissionSets.Add(new IamPermissionSet
        {
            Id = permissionSetId,
            LifecycleStatus = "published",
            PermissionsJson = "[\"users.export\",\"unknown.deny\"]"
        });
        db.IamPermissionSetAssignments.Add(new IamPermissionSetAssignment
        {
            PermissionSetId = permissionSetId,
            PrincipalType = "group",
            PrincipalId = groupId,
            Status = "active",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
        db.BreakGlassRequests.Add(new BreakGlassRequest
        {
            SubjectUserId = userId,
            PermissionCode = "users.breakglass",
            Status = "approved",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, CreateUserManager(), CreateRedis(false));

        dynamic role = await InvokeAsync(service, "CheckPermission", "CheckPermissionRequest", request =>
        {
            Set(request, "UserId", userId.ToString());
            Set(request, "PermissionCode", "users.read");
        });
        dynamic assigned = await InvokeAsync(service, "CheckPermission", "CheckPermissionRequest", request =>
        {
            Set(request, "UserId", userId.ToString());
            Set(request, "PermissionCode", "users.export");
        });
        dynamic emergency = await InvokeAsync(service, "CheckPermission", "CheckPermissionRequest", request =>
        {
            Set(request, "UserId", userId.ToString());
            Set(request, "PermissionCode", "users.breakglass");
        });
        dynamic rejected = await InvokeAsync(service, "CheckPermission", "CheckPermissionRequest", request =>
        {
            Set(request, "UserId", userId.ToString());
            Set(request, "PermissionCode", "unknown.deny");
        });

        ((bool)role.HasPermission).Should().BeTrue();
        ((bool)assigned.HasPermission).Should().BeTrue();
        ((bool)emergency.HasPermission).Should().BeTrue();
        ((bool)rejected.HasPermission).Should().BeFalse();
    }

    [Fact]
    public async Task CheckPermission_denies_all_permissions_when_boundary_json_is_malformed()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
        db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionCode = "users.read" });
        db.IamPermissionBoundaries.Add(new IamPermissionBoundary
        {
            PrincipalId = userId,
            PrincipalType = "human",
            AllowedPermissionsJson = "not-json",
            IsActive = true
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, CreateUserManager(), CreateRedis(false));

        dynamic response = await InvokeAsync(service, "CheckPermission", "CheckPermissionRequest", request =>
        {
            Set(request, "UserId", userId.ToString());
            Set(request, "PermissionCode", "users.read");
        });

        ((bool)response.HasPermission).Should().BeFalse();
    }

    [Fact]
    public async Task RevokeUserTokens_unknown_user_reports_zero_tokens()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager();
        userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((User?)null);
        var service = CreateService(db, userManager, CreateRedis(false));

        dynamic response = await InvokeAsync(service, "RevokeUserTokens", "RevokeUserTokensRequest", request =>
        {
            Set(request, "UserId", "missing");
            Set(request, "Reason", "test");
        });

        ((int)response.TokensRevoked).Should().Be(0);
        userManager.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<User>()), Times.Never);
    }

    private static GrpcIdentityService CreateService(
        IdentityDbContext db,
        Mock<UserManager<User>> userManager,
        Mock<IConnectionMultiplexer> redis) =>
        new(db, userManager.Object, redis.Object, NullLogger<GrpcIdentityService>.Instance);

    private static IdentityDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"grpc-tests-{Guid.NewGuid():N}")
            .Options);

    private static Mock<UserManager<User>> CreateUserManager() => new(
        new Mock<IUserStore<User>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    private static Mock<IConnectionMultiplexer> CreateRedis(bool blacklisted)
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(blacklisted);
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        return redis;
    }

    private static async Task<object> InvokeAsync(object service, string methodName, string requestName, Action<object>? configure = null)
    {
        var requestType = service.GetType().Assembly.GetType($"His.Hope.Identity.Grpc.{requestName}")!;
        var request = Activator.CreateInstance(requestType)!;
        configure?.Invoke(request);
        var method = service.GetType().GetMethod(methodName)!;
        var task = (Task)method.Invoke(service, [request, null!])!;
        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static void Set(object target, string propertyName, string value) =>
        target.GetType().GetProperty(propertyName)!.SetValue(target, value);

    private static string JwtWithClaims(string json)
    {
        static string Encode(string value) =>
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return Encode("{}") + "." + Encode(json) + ".signature";
    }
}
