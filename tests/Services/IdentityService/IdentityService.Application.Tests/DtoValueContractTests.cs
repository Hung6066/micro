using FluentAssertions;
using His.Hope.IdentityService.Application.DTOs;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class DtoValueContractTests
{
    [Fact]
    public void Optional_authentication_values_default_to_null_without_mutating_required_values()
    {
        var login = new LoginRequest(Email: "user@example.test", Password: "Password1!");
        login.Username.Should().BeNull();
        login.Email.Should().Be("user@example.test");
        login.Password.Should().Be("Password1!");
        login.DeviceInfo.Should().BeNull();
        login.IpAddress.Should().BeNull();
        login.UserAgent.Should().BeNull();

        var registration = new RegisterRequest(Email: "user@example.test", Password: "Password1!");
        registration.Username.Should().BeNull();
        registration.Email.Should().Be("user@example.test");
        registration.Password.Should().Be("Password1!");
        registration.FirstName.Should().BeNull();
        registration.LastName.Should().BeNull();
        registration.DeviceInfo.Should().BeNull();
        registration.IpAddress.Should().BeNull();
    }

    [Fact]
    public void Role_and_client_contracts_preserve_nullable_collections_and_security_defaults()
    {
        var role = new RoleDto(Guid.NewGuid(), "Reader", null, false, DateTime.UtcNow, null);
        role.Description.Should().BeNull();
        role.Permissions.Should().BeNull();
        role.Owner.Should().Be("identity-service");
        role.Version.Should().Be(1);
        role.RiskTier.Should().Be("standard");
        role.ReviewCadenceDays.Should().Be(180);
        role.LifecycleStatus.Should().Be("active");
        role.PublishedAt.Should().BeNull();
        role.PublishedBy.Should().BeNull();

        var request = new CreateClientRequest("client", "Client", "public", [], null, null, [], null);
        request.GrantTypes.Should().BeEmpty();
        request.RedirectUris.Should().BeNull();
        request.PostLogoutRedirectUris.Should().BeNull();
        request.Scopes.Should().BeEmpty();
        request.FacilityId.Should().BeNull();
        request.Jwks.Should().BeNull();

        var dynamic = new DynamicClientRegistrationRequest("Client", [], null, null, null);
        dynamic.RedirectUris.Should().BeEmpty();
        dynamic.PostLogoutRedirectUris.Should().BeNull();
        dynamic.GrantTypes.Should().BeNull();
        dynamic.Scopes.Should().BeNull();
        dynamic.TokenEndpointAuthMethod.Should().BeNull();
        dynamic.Jwks.Should().BeNull();
    }

    [Fact]
    public void Permission_descriptor_values_are_safe_for_known_and_unknown_codes()
    {
        var known = new PermissionDto("admin.users.read", "Read users", "users", null, false);
        known.Owner.Should().Be("identity-service");
        known.Version.Should().BeGreaterThan(0);
        known.RiskTier.Should().NotBeNullOrWhiteSpace();
        known.RequiredAssurance.Should().NotBeNullOrWhiteSpace();
        known.AuditClass.Should().NotBeNullOrWhiteSpace();

        var unknown = new PermissionDto("unknown.permission", "Unknown", "unknown", null, false);
        unknown.Owner.Should().Be("unknown");
        unknown.Version.Should().Be(1);
        unknown.RiskTier.Should().Be("standard");
        unknown.RequiredAssurance.Should().Be("standard");
        unknown.AuditClass.Should().Be("authorization");
        unknown.IsDeprecated.Should().BeFalse();
        unknown.ReplacedBy.Should().BeNull();
    }

    [Fact]
    public void Recovery_and_session_values_round_trip_boundary_inputs()
    {
        var refresh = new RefreshTokenRequest("", "", "", "");
        refresh.AccessToken.Should().BeEmpty();
        refresh.RefreshToken.Should().BeEmpty();
        refresh.DeviceInfo.Should().BeEmpty();
        refresh.IpAddress.Should().BeEmpty();

        var session = new SessionInfo("", "", "", DateTimeOffset.MinValue, DateTimeOffset.MaxValue, false);
        session.SessionId.Should().BeEmpty();
        session.DeviceInfo.Should().BeEmpty();
        session.IpAddress.Should().BeEmpty();
        session.CreatedAt.Should().Be(DateTimeOffset.MinValue);
        session.LastActivity.Should().Be(DateTimeOffset.MaxValue);
        session.IsCurrent.Should().BeFalse();
    }
}
