using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Domain.Tests;

public class AuditLogTests
{
    [Fact]
    public void Create_WithRequiredFields_ShouldSetProperties()
    {
        var log = new AuditLog
        {
            UserId = "user-123",
            Action = "READ",
            ResourceType = "Patient",
            ResourceId = "pat-456",
            Details = "Accessed patient demographics"
        };

        log.Id.Should().NotBeEmpty();
        log.UserId.Should().Be("user-123");
        log.Action.Should().Be("READ");
        log.ResourceType.Should().Be("Patient");
        log.ResourceId.Should().Be("pat-456");
        log.Details.Should().Be("Accessed patient demographics");
        log.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        log.IpAddress.Should().BeNull();
        log.UserAgent.Should().BeNull();
        log.UserName.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllFields_ShouldSetCorrectly()
    {
        var log = new AuditLog
        {
            UserId = "user-456",
            UserName = "Dr. Smith",
            Action = "UPDATE",
            ResourceType = "Encounter",
            ResourceId = "enc-789",
            Details = "Updated diagnosis",
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0",
            Timestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        log.UserName.Should().Be("Dr. Smith");
        log.IpAddress.Should().Be("192.168.1.100");
        log.UserAgent.Should().Be("Mozilla/5.0");
        log.Timestamp.Should().Be(new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("CREATE")]
    [InlineData("READ")]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    public void WithDifferentActions_ShouldStoreCorrectly(string action)
    {
        var log = new AuditLog
        {
            UserId = "user-1",
            Action = action,
            ResourceType = "Patient"
        };

        log.Action.Should().Be(action);
    }

    [Fact]
    public void Timestamp_DefaultsToUtcNow()
    {
        var log = new AuditLog
        {
            UserId = "user-1",
            Action = "READ",
            ResourceType = "Patient"
        };

        log.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Id_ShouldBeUnique()
    {
        var log1 = new AuditLog { UserId = "u1", Action = "READ", ResourceType = "Patient" };
        var log2 = new AuditLog { UserId = "u2", Action = "WRITE", ResourceType = "Encounter" };

        log1.Id.Should().NotBe(log2.Id);
    }

    [Fact]
    public void AuditLog_WithNullOptionalFields_ShouldNotThrow()
    {
        var log = new AuditLog
        {
            UserId = "user-1",
            Action = "READ",
            ResourceType = "Patient"
        };

        log.Invoking(l => l.UserName = null).Should().NotThrow();
        log.Invoking(l => l.ResourceId = null).Should().NotThrow();
        log.Invoking(l => l.Details = null).Should().NotThrow();
        log.Invoking(l => l.IpAddress = null).Should().NotThrow();
        log.Invoking(l => l.UserAgent = null).Should().NotThrow();
    }
}
