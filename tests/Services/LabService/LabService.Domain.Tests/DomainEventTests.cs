using FluentAssertions;
using His.Hope.LabService.Domain.Events;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Tests;

public class DomainEventTests
{
    [Fact]
    public void LabOrderCreatedDomainEvent_ShouldSetProperties()
    {
        var labOrderId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var domainEvent = new LabOrderCreatedDomainEvent(labOrderId, patientId, providerId);

        domainEvent.LabOrderId.Should().Be(labOrderId);
        domainEvent.PatientId.Should().Be(patientId);
        domainEvent.ProviderId.Should().Be(providerId);
        domainEvent.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void LabOrderCreatedDomainEvent_ShouldImplementIDomainEvent()
    {
        var domainEvent = new LabOrderCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        domainEvent.Should().BeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void LabOrderSubmittedDomainEvent_ShouldSetProperties()
    {
        var labOrderId = Guid.NewGuid();

        var domainEvent = new LabOrderSubmittedDomainEvent(labOrderId);

        domainEvent.LabOrderId.Should().Be(labOrderId);
        domainEvent.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void LabOrderSubmittedDomainEvent_ShouldImplementIDomainEvent()
    {
        var domainEvent = new LabOrderSubmittedDomainEvent(Guid.NewGuid());

        domainEvent.Should().BeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void LabOrderCancelledDomainEvent_ShouldSetProperties()
    {
        var labOrderId = Guid.NewGuid();
        var reason = "Patient request";

        var domainEvent = new LabOrderCancelledDomainEvent(labOrderId, reason);

        domainEvent.LabOrderId.Should().Be(labOrderId);
        domainEvent.Reason.Should().Be(reason);
        domainEvent.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void LabOrderCancelledDomainEvent_WithEmptyReason_ShouldSetEmpty()
    {
        var domainEvent = new LabOrderCancelledDomainEvent(Guid.NewGuid(), "");

        domainEvent.Reason.Should().Be("");
    }

    [Fact]
    public void LabTestResultRecordedDomainEvent_ShouldSetProperties()
    {
        var labOrderId = Guid.NewGuid();
        var labTestId = Guid.NewGuid();
        var value = "5.5";

        var domainEvent = new LabTestResultRecordedDomainEvent(labOrderId, labTestId, value);

        domainEvent.LabOrderId.Should().Be(labOrderId);
        domainEvent.LabTestId.Should().Be(labTestId);
        domainEvent.Value.Should().Be(value);
        domainEvent.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void LabTestResultRecordedDomainEvent_ShouldImplementIDomainEvent()
    {
        var domainEvent = new LabTestResultRecordedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), "5.5");

        domainEvent.Should().BeAssignableTo<IDomainEvent>();
    }
}
