using FluentAssertions;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.ValueObjects;

namespace His.Hope.LabService.Domain.Tests;

public class CriticalAlertTests
{
    private static CriticalAlert CreateAlert()
    {
        return CriticalAlert.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            CriticalAlertTriggerType.CriticalFlag,
            "Critical flag CRITICAL_HIGH",
            "18.5",
            "x10^9/L",
            null,
            "system",
            "System");
    }

    [Fact]
    public void Create_ShouldCreateOpenAlertWithAuditEntry()
    {
        var alert = CreateAlert();

        alert.Status.Should().Be(CriticalAlertStatus.Open);
        alert.AuditEntries.Should().ContainSingle();
        alert.AuditEntries.Single().Action.Should().Be("Created");
        alert.AuditEntries.Single().ActorUserId.Should().Be("system");
        alert.AuditEntries.Single().ActorDisplayName.Should().Be("System");
    }

    [Fact]
    public void UpdateObservation_ShouldAppendAuditEntry()
    {
        var alert = CreateAlert();

        alert.UpdateObservation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CriticalAlertTriggerType.Both,
            "Updated critical result",
            "19.1",
            "x10^9/L",
            10.0m,
            "user-1",
            "Dr. Jones");

        alert.TriggerType.Should().Be(CriticalAlertTriggerType.Both);
        alert.AuditEntries.Should().HaveCount(2);
        alert.AuditEntries.Last().Action.Should().Be("Updated");
        alert.AuditEntries.Last().ActorUserId.Should().Be("user-1");
        alert.AuditEntries.Last().ActorDisplayName.Should().Be("Dr. Jones");
    }

    [Fact]
    public void Resolve_ShouldAppendAuditEntryAndSetState()
    {
        var alert = CreateAlert();

        alert.Resolve("user-2", "Dr. Smith", "Correction to normal value");

        alert.Status.Should().Be(CriticalAlertStatus.Resolved);
        alert.ResolvedByUserId.Should().Be("user-2");
        alert.ResolvedByDisplayName.Should().Be("Dr. Smith");
        alert.AuditEntries.Should().HaveCount(2);
        alert.AuditEntries.Last().Action.Should().Be("Resolved");
    }

    [Fact]
    public void Acknowledge_ShouldAppendAuditEntryAndSetState()
    {
        var alert = CreateAlert();

        alert.Acknowledge("user-3", "Dr. Lee", "Reviewed");

        alert.Status.Should().Be(CriticalAlertStatus.Acknowledged);
        alert.AcknowledgedByUserId.Should().Be("user-3");
        alert.AcknowledgedByDisplayName.Should().Be("Dr. Lee");
        alert.AuditEntries.Should().HaveCount(2);
        alert.AuditEntries.Last().Action.Should().Be("Acknowledged");
    }
}
