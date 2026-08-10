using FluentAssertions;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Events;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.ClinicalService.Domain.Tests;

public class ClinicalDomainEventsTests
{
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();

    [Fact]
    public void StartEncounter_RaisesEncounterStartedDomainEvent()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);

        var domainEvent = encounter.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<EncounterStartedDomainEvent>().Subject;

        domainEvent.EncounterId.Should().Be(encounter.Id.Value);
        domainEvent.PatientId.Should().Be(PatientId);
        domainEvent.ProviderId.Should().Be(ProviderId);
        domainEvent.EncounterType.Should().Be(EncounterType.Outpatient);
        domainEvent.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordVitals_RaisesVitalsRecordedDomainEvent()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        encounter.ClearDomainEvents();

        encounter.RecordVitals(37.0m, 72, 16, 120, 80, 98.0m, 175.0m, 70.0m, 22.9m);

        var domainEvent = encounter.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<VitalsRecordedDomainEvent>().Subject;

        domainEvent.EncounterId.Should().Be(encounter.Id.Value);
        domainEvent.PatientId.Should().Be(PatientId);
        domainEvent.Temperature.Should().Be(37.0m);
        domainEvent.HeartRate.Should().Be(72);
        domainEvent.SystolicBP.Should().Be(120);
        domainEvent.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordVitals_WithNullValues_RaisesEventWithNulls()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        encounter.ClearDomainEvents();

        encounter.RecordVitals(null, null, null, null, null, null, null, null, null);

        var domainEvent = encounter.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<VitalsRecordedDomainEvent>().Subject;

        domainEvent.Temperature.Should().BeNull();
        domainEvent.HeartRate.Should().BeNull();
    }

    [Fact]
    public void AddDiagnosis_RaisesDiagnosisAddedDomainEvent()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        encounter.ClearDomainEvents();
        var diagnosis = new Diagnosis("Hypertension", "I10", true, "Primary");

        encounter.AddDiagnosis(diagnosis);

        var domainEvent = encounter.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DiagnosisAddedDomainEvent>().Subject;

        domainEvent.EncounterId.Should().Be(encounter.Id.Value);
        domainEvent.ConditionName.Should().Be("Hypertension");
        domainEvent.Icd10Code.Should().Be("I10");
        domainEvent.IsPrimary.Should().BeTrue();
        domainEvent.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CompleteEncounter_RaisesEncounterCompletedDomainEvent()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        encounter.ClearDomainEvents();

        encounter.Complete();

        var domainEvent = encounter.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<EncounterCompletedDomainEvent>().Subject;

        domainEvent.EncounterId.Should().Be(encounter.Id.Value);
        domainEvent.PatientId.Should().Be(PatientId);
        domainEvent.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MultipleActions_ShouldAccumulateEvents()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        encounter.ClearDomainEvents();

        encounter.RecordVitals(37.0m, 72, 16, 120, 80, 98.0m, null, null, null);
        encounter.AddDiagnosis(new Diagnosis("Hypertension", "I10", true, null));
        encounter.Complete();

        encounter.DomainEvents.Should().HaveCount(3);
        var eventsList = encounter.DomainEvents.ToList();
        eventsList[0].Should().BeOfType<VitalsRecordedDomainEvent>();
        eventsList[1].Should().BeOfType<DiagnosisAddedDomainEvent>();
        eventsList[2].Should().BeOfType<EncounterCompletedDomainEvent>();
    }

    [Fact]
    public void EncounterStartedDomainEvent_WithAppointmentId_InConstructor()
    {
        var appointmentId = Guid.NewGuid();
        var occurredOn = DateTime.UtcNow;
        var evt = new EncounterStartedDomainEvent(
            Guid.NewGuid(), PatientId, ProviderId, appointmentId,
            EncounterType.Telehealth, occurredOn);

        evt.AppointmentId.Should().Be(appointmentId);
        evt.EncounterType.Should().Be(EncounterType.Telehealth);
    }

    [Fact]
    public void EncounterCompletedDomainEvent_ShouldSetCompletedAt()
    {
        var completedAt = DateTime.UtcNow;
        var evt = new EncounterCompletedDomainEvent(
            Guid.NewGuid(), PatientId, completedAt, DateTime.UtcNow);

        evt.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void DomainEvents_ShouldBeRecords()
    {
        var evt1 = new VitalsRecordedDomainEvent(
            Guid.NewGuid(), PatientId, 37.0m, 72, 16, 120, 80, 98.0m, DateTime.UtcNow);
        var evt2 = new VitalsRecordedDomainEvent(
            evt1.EncounterId, evt1.PatientId, evt1.Temperature, evt1.HeartRate,
            evt1.RespiratoryRate, evt1.SystolicBP, evt1.DiastolicBP,
            evt1.OxygenSaturation, evt1.OccurredOn);

        evt1.Should().Be(evt2);
        evt1.GetHashCode().Should().Be(evt2.GetHashCode());
    }
}
