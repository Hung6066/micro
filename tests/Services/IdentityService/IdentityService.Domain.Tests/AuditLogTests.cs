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

    [Fact]
    public void IntegrityHash_ShouldBeStableForTheSameCanonicalEntry()
    {
        var log = new AuditLog
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId = "user-1",
            Action = "READ",
            ResourceType = "Patient",
            ResourceId = "patient-1",
            IntegritySequence = 1,
            Timestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        var first = AuditLogIntegrity.ComputeHash(log, null);
        var second = AuditLogIntegrity.ComputeHash(log, null);

        first.Should().Be(second);
        first.Should().Be("4081fb2a50c31d89da485d10ade10d42fb1ccbb96108fdace80c0e08d795e461");
    }

    [Fact]
    public void VerifyChain_ShouldRejectTamperedEntry()
    {
        var first = new AuditLog
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId = "user-1", Action = "READ", ResourceType = "Patient",
            IntegritySequence = 1,
            Timestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc)
        };
        first.IntegrityHash = AuditLogIntegrity.ComputeHash(first, null);

        var second = new AuditLog
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UserId = "user-1", Action = "UPDATE", ResourceType = "Patient",
            IntegritySequence = 2,
            Timestamp = new DateTime(2024, 6, 15, 10, 31, 0, DateTimeKind.Utc),
            PreviousIntegrityHash = first.IntegrityHash
        };
        second.IntegrityHash = AuditLogIntegrity.ComputeHash(second, second.PreviousIntegrityHash);
        second.Action = "DELETE";

        AuditLogIntegrity.VerifyChain([first, second]).Should().BeFalse();
    }

    [Fact]
    public void VerifyChainDetailed_ShouldIdentifyTamperedEntry()
    {
        var entry = new AuditLog
        {
            UserId = "user-1",
            Action = "READ",
            ResourceType = "Patient",
            IntegritySequence = 1,
            IntegrityHash = "not-the-computed-hash"
        };

        var result = AuditLogIntegrity.VerifyChainDetailed([entry]);

        result.IsValid.Should().BeFalse();
        result.EntriesChecked.Should().Be(0);
        result.InvalidIndex.Should().Be(0);
        result.FailureReason.Should().Be("hash-mismatch");
        result.ActualSequence.Should().Be(1);
    }

    [Fact]
    public void VerifyChainDetailed_ShouldReportLegacyEntryWithoutSequence()
    {
        var result = AuditLogIntegrity.VerifyChainDetailed([new AuditLog
        {
            UserId = "legacy-user",
            Action = "READ",
            ResourceType = "Patient"
        }]);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be("missing-sequence");
        result.EntriesChecked.Should().Be(0);
    }
}
