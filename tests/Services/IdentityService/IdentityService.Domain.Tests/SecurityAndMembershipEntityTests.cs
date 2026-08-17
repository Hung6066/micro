using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Domain.Tests;

public sealed class SecurityAndMembershipEntityTests
{
    [Fact]
    public void User_client_certificate_has_secure_defaults_and_round_trips_all_fields()
    {
        var certificate = new UserClientCertificate();

        certificate.Id.Should().NotBeEmpty();
        certificate.Thumbprint.Should().BeEmpty();
        certificate.Subject.Should().BeNull();
        certificate.RevokedAt.Should().BeNull();
        certificate.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddMinutes(-2);
        var revokedAt = DateTime.UtcNow.AddMinutes(-1);
        var user = new User { Id = userId, UserName = "certificate-user", Email = "certificate@example.test" };

        certificate.UserId = userId;
        certificate.Thumbprint = "A1B2C3D4";
        certificate.Subject = "CN=certificate-user";
        certificate.NotAfter = DateTime.UtcNow.AddYears(1);
        certificate.RevokedAt = revokedAt;
        certificate.CreatedAt = createdAt;
        certificate.User = user;

        certificate.UserId.Should().Be(userId);
        certificate.Thumbprint.Should().Be("A1B2C3D4");
        certificate.Subject.Should().Be("CN=certificate-user");
        certificate.NotAfter.Should().BeAfter(DateTime.UtcNow);
        certificate.RevokedAt.Should().Be(revokedAt);
        certificate.CreatedAt.Should().Be(createdAt);
        certificate.User.Should().BeSameAs(user);
    }

    [Fact]
    public void User_facility_defaults_to_active_and_round_trips_revocation_state()
    {
        var membership = new UserFacility();

        membership.UserId.Should().BeEmpty();
        membership.FacilityId.Should().BeEmpty();
        membership.IsPrimary.Should().BeFalse();
        membership.IsActive.Should().BeTrue();
        membership.RevokedAt.Should().BeNull();
        membership.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var user = new User { Id = Guid.NewGuid(), UserName = "facility-user", Email = "facility@example.test" };
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var revokedAt = DateTime.UtcNow;

        membership.UserId = user.Id;
        membership.User = user;
        membership.FacilityId = "facility-01";
        membership.IsPrimary = true;
        membership.IsActive = false;
        membership.CreatedAt = createdAt;
        membership.RevokedAt = revokedAt;

        membership.UserId.Should().Be(user.Id);
        membership.User.Should().BeSameAs(user);
        membership.FacilityId.Should().Be("facility-01");
        membership.IsPrimary.Should().BeTrue();
        membership.IsActive.Should().BeFalse();
        membership.CreatedAt.Should().Be(createdAt);
        membership.RevokedAt.Should().Be(revokedAt);
    }

    [Fact]
    public void Mobile_telemetry_event_defaults_to_bounded_empty_payload()
    {
        var telemetry = new MobileTelemetryEvent();

        telemetry.Id.Should().NotBeEmpty();
        telemetry.EventType.Should().BeEmpty();
        telemetry.Name.Should().BeEmpty();
        telemetry.AppVersion.Should().BeEmpty();
        telemetry.Platform.Should().BeEmpty();
        telemetry.Message.Should().BeNull();
        telemetry.Stack.Should().BeNull();
        telemetry.Route.Should().BeNull();
        telemetry.DurationMs.Should().BeNull();
        telemetry.MetadataJson.Should().BeNull();
        telemetry.CorrelationId.Should().BeNull();
        telemetry.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        telemetry.EventType = "crash";
        telemetry.Name = "UnhandledException";
        telemetry.Message = "network unavailable";
        telemetry.Stack = "at Mobile.App.Start()";
        telemetry.Route = "/login";
        telemetry.AppVersion = "1.2.3";
        telemetry.Platform = "android";
        telemetry.DurationMs = 125.5;
        telemetry.MetadataJson = "{\"screen\":\"login\"}";
        telemetry.CorrelationId = "corr-123";

        telemetry.EventType.Should().Be("crash");
        telemetry.Name.Should().Be("UnhandledException");
        telemetry.Message.Should().Be("network unavailable");
        telemetry.Stack.Should().Contain("Mobile.App");
        telemetry.Route.Should().Be("/login");
        telemetry.AppVersion.Should().Be("1.2.3");
        telemetry.Platform.Should().Be("android");
        telemetry.DurationMs.Should().Be(125.5);
        telemetry.MetadataJson.Should().Contain("login");
        telemetry.CorrelationId.Should().Be("corr-123");
    }

    [Fact]
    public void Security_signal_outbox_defaults_to_immediately_available_pending_delivery()
    {
        var outbox = new SecuritySignalOutbox();

        outbox.Id.Should().NotBeEmpty();
        outbox.EventType.Should().BeEmpty();
        outbox.Subject.Should().BeEmpty();
        outbox.PayloadJson.Should().Be("{}");
        outbox.Attempts.Should().Be(0);
        outbox.DispatchedAt.Should().BeNull();
        outbox.LastError.Should().BeNull();
        outbox.AvailableAt.Should().BeOnOrAfter(outbox.CreatedAt);
        outbox.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var createdAt = DateTime.UtcNow.AddMinutes(-3);
        var availableAt = DateTime.UtcNow.AddMinutes(-2);
        var dispatchedAt = DateTime.UtcNow.AddMinutes(-1);
        outbox.CreatedAt = createdAt;
        outbox.AvailableAt = availableAt;
        outbox.EventType = "session.revoked";
        outbox.Subject = "user-123";
        outbox.PayloadJson = "{\"sub\":\"user-123\"}";
        outbox.Attempts = 2;
        outbox.LastError = "temporary downstream failure";
        outbox.DispatchedAt = dispatchedAt;

        outbox.CreatedAt.Should().Be(createdAt);
        outbox.AvailableAt.Should().Be(availableAt);
        outbox.EventType.Should().Be("session.revoked");
        outbox.Subject.Should().Be("user-123");
        outbox.PayloadJson.Should().Contain("user-123");
        outbox.Attempts.Should().Be(2);
        outbox.LastError.Should().Contain("downstream");
        outbox.DispatchedAt.Should().Be(dispatchedAt);
    }
}
