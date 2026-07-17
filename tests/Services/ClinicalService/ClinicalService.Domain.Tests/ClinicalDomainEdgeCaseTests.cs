using FluentAssertions;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.ClinicalService.Domain.Tests;

public class ClinicalDomainEdgeCaseTests
{
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();

    [Fact]
    public void Complete_WithAlreadyCompletedEncounter_ShouldStayCompleted()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        encounter.Complete();
        encounter.Complete();
        encounter.Status.Should().Be(EncounterStatus.Completed);
    }

    [Fact]
    public void RecordAssessment_WithEmptyString_ShouldSetAssessment()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        encounter.RecordAssessment("");
        encounter.Assessment.Should().Be("");
    }

    [Fact]
    public void RecordPlan_WithEmptyString_ShouldSetPlan()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        encounter.RecordPlan("");
        encounter.Plan.Should().Be("");
    }

    [Fact]
    public void MultipleActions_ShouldUpdateTimestampEachTime()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        encounter.RecordVitals(37.0m, 72, 16, 120, 80, 98.0m, null, null, null);
        var firstUpdate = encounter.UpdatedAt;
        encounter.RecordAssessment("Patient stable");
        var secondUpdate = encounter.UpdatedAt;
        secondUpdate.Should().BeAfter(firstUpdate ?? DateTime.MinValue);
    }

    [Fact]
    public void Procedure_WithNullNotes_ShouldSetNull()
    {
        var procedure = new Procedure("Chest X-Ray", "71046", DateTime.UtcNow, null);
        procedure.Notes.Should().BeNull();
    }

    [Fact]
    public void Diagnosis_Equality_DifferentValues_ShouldNotBeEqual()
    {
        var d1 = new Diagnosis("Hypertension", "I10", true, null);
        var d2 = new Diagnosis("Diabetes", "E11", false, null);
        d1.Should().NotBe(d2);
    }

    [Fact]
    public void VitalSigns_WithPartialValues_ShouldSetCorrectly()
    {
        var vitals = new VitalSigns(38.5m, 88, null, null, null, null, null, null, null);
        vitals.Temperature.Should().Be(38.5m);
        vitals.HeartRate.Should().Be(88);
        vitals.RespiratoryRate.Should().BeNull();
        vitals.SystolicBP.Should().BeNull();
    }

    [Fact]
    public void HistoryPresentIllness_WithNullFields_ShouldHandleGracefully()
    {
        var hpi = new HistoryPresentIllness(null, null, null, null, null, null, null);
        hpi.Onset.Should().BeNull();
        hpi.Location.Should().BeNull();
    }

    [Fact]
    public void EncounterId_Equality_OperatorOverloads()
    {
        var guid = Guid.NewGuid();
        var id1 = new EncounterId(guid);
        var id2 = new EncounterId(guid);
        (id1 == id2).Should().BeTrue();
        (id1 != id2).Should().BeFalse();
    }

    [Fact]
    public void EncounterType_FromName_CaseSensitive_ShouldWork()
    {
        var type = EncounterType.FromName("Emergency");
        type.Should().Be(EncounterType.Emergency);
    }

    [Fact]
    public void EncounterStatus_CompareTo_ShouldOrderCorrectly()
    {
        var inProgress = EncounterStatus.InProgress;
        var completed = EncounterStatus.Completed;
        inProgress.CompareTo(completed).Should().BePositive();
    }

    [Fact]
    public void Encounter_WithNoChiefComplaint_ShouldHaveNullChiefComplaint()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        encounter.ChiefComplaint.Should().BeNull();
    }

    [Fact]
    public void Start_ShouldHaveEmptyDiagnosesAndProcedures()
    {
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        encounter.Diagnoses.Should().BeEmpty();
        encounter.Procedures.Should().BeEmpty();
    }
}
