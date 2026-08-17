using FluentAssertions;
using His.Hope.IdentityService.Application.DTOs;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class DtoContractTests
{
    [Fact]
    public void Identity_and_client_contracts_round_trip_required_fields()
    {
        var user = new UserDto(Guid.NewGuid(), "user", "user@example.test", "First", "Last", null, "Last First", null, null, ["Admin"], ["users.read"]);
        var permission = new PermissionDto("users.read", "Read users", "users", "Read access", false);
        var role = new RoleDto(Guid.NewGuid(), "Admin", "Administrator", false, DateTime.UtcNow, [permission]);
        var detail = new UserDetailDto(Guid.NewGuid(), "user", "user@example.test", null, "First", "Last", null, "Last First", null, null, true, DateTime.UtcNow, null, ["Admin"]);

        new LoginRequest(Email: user.Email, Password: "Password1!").Email.Should().Be(user.Email);
        new RegisterRequest(Username: user.Username, Email: user.Email, Password: "Password1!").Username.Should().Be("user");
        new LdapLoginRequest("user", "secret").UserName.Should().Be("user");
        new RefreshTokenRequest("access", "refresh").RefreshToken.Should().Be("refresh");
        new TokenResponse("access", "refresh", DateTime.UtcNow, user).User.Should().Be(user);
        new MfaEnrollResponse("secret", "otpauth://", ["recovery"]).RecoveryCodes.Should().Contain("recovery");
        new MfaVerifyRequest("123456").Code.Should().Be("123456");
        new MfaVerifyResponse("access", "refresh", DateTime.UtcNow, user).User.Should().Be(user);
        new MfaRecoverRequest("recovery").RecoveryCode.Should().Be("recovery");

        new CreateUserRequest("user", user.Email, "Password1!", "First", "Last", null, null, null, null, "Admin").Role.Should().Be("Admin");
        new UpdateUserRequest("First", "Last", user.Email, null, "Admin", true).IsActive.Should().BeTrue();
        new AssignRolesRequest(["admin"]).RoleIds.Should().Contain("admin");
        role.Permissions.Should().ContainSingle().Which.Code.Should().Be("users.read");
        new CreateRoleRequest("Admin", "Administrator", ["users.read"]).Name.Should().Be("Admin");
        new UpdateRoleRequest("Admin", null, null).Name.Should().Be("Admin");
        permission.Owner.Should().NotBeNullOrWhiteSpace();
        new SystemSettingDto("mfa", "required", null, "security", DateTime.UtcNow, "admin").Key.Should().Be("mfa");
        new UpdateSettingRequest("required", null).Value.Should().Be("required");
        new BulkUpdateSettingItem("mfa", "required").Key.Should().Be("mfa");
        new AuditLogDto(Guid.NewGuid(), "user", "User", "read", "User", null, null, null, null, DateTime.UtcNow).Action.Should().Be("read");
        detail.FullName.Should().Be("Last First");
    }

    [Fact]
    public void Client_recovery_and_bulk_contracts_preserve_values()
    {
        var client = new CreateClientRequest("mobile", "Mobile", "public", ["authorization_code"], ["https://app/callback"], [], ["openid"], null);
        client.GrantTypes.Should().Contain("authorization_code");
        new UpdateClientRequest("Updated", null, null, null, ["openid"], true).IsActive.Should().BeTrue();
        new ClientResponse("id", "mobile", "Mobile", "public", ["authorization_code"], [], [], ["openid"], true, null, DateTime.UtcNow, null).ClientId.Should().Be("mobile");
        new ClientSecretResponse("mobile", "secret", "created").Message.Should().Be("created");
        new ClientOnboardingResponse("mobile", "Mobile", "issuer", "auth", "token", "jwks", ["authorization_code"], ["openid"], "none").Issuer.Should().Be("issuer");
        new DynamicClientRegistrationRequest("Mobile", ["https://app/callback"], null, null, ["openid"]).ClientName.Should().Be("Mobile");
        new DynamicClientRegistrationResponse("mobile", null, "Mobile", ["https://app/callback"], ["authorization_code"], ["openid"], "none").ClientId.Should().Be("mobile");

        new ForgotPasswordRequest("user@example.test").Email.Should().Be("user@example.test");
        new ResetPasswordRequest("user@example.test", "token", "new-password").Token.Should().Be("token");
        new ChangePasswordRequest("old", "new").NewPassword.Should().Be("new");
        new VerifyEmailRequest("user@example.test", "token").Token.Should().Be("token");
        new SessionInfo("session", "device", "127.0.0.1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true).IsCurrent.Should().BeTrue();

        var import = new BulkImportRequest([new BulkUserRecord("user", "user@example.test", "First", "Last")]);
        import.Users.Should().ContainSingle().Which.UserName.Should().Be("user");
        new BulkImportResult(1, 1, 0, 0, [new BulkImportError("user", "none")]).Succeeded.Should().Be(1);
    }
}
