using FluentAssertions;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.ClinicalService.Domain.Tests;

public class EncounterTests
{
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();

    [Fact]
    public void Start_WithValidParameters_ShouldCreateInProgressEncounter()
    {
        // Act
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);

        // Assert
        encounter.Should().NotBeNull();
        encounter.Id.Should().NotBeNull();
        encounter.PatientId.Should().Be(PatientId);
        encounter.ProviderId.Should().Be(ProviderId);
        encounter.EncounterType.Should().Be(EncounterType.Outpatient);
        encounter.Status.Should().Be(EncounterStatus.InProgress);
        encounter.EncounterDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        encounter.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        encounter.ChiefComplaint.Should().BeNull();
        encounter.Diagnoses.Should().BeEmpty();
        encounter.Procedures.Should().BeEmpty();
    }

    [Fact]
    public void Start_WithEmergencyType_ShouldCreateCorrectEncounter()
    {
        // Act
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Emergency);

        // Assert
        encounter.EncounterType.Should().Be(EncounterType.Emergency);
    }

    [Fact]
    public void Start_WithTelehealthType_ShouldCreateCorrectEncounter()
    {
        // Act
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Telehealth);

        // Assert
        encounter.EncounterType.Should().Be(EncounterType.Telehealth);
    }

    [Fact]
    public void Start_WithInpatientType_ShouldCreateCorrectEncounter()
    {
        // Act
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Inpatient);

        // Assert
        encounter.EncounterType.Should().Be(EncounterType.Inpatient);
    }

    [Fact]
    public void Start_WithFollowUpType_ShouldCreateCorrectEncounter()
    {
        // Act
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.FollowUp);

        // Assert
        encounter.EncounterType.Should().Be(EncounterType.FollowUp);
    }

    [Fact]
    public void Start_WithAnnualWellnessType_ShouldCreateCorrectEncounter()
    {
        // Act
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.AnnualWellness);

        // Assert
        encounter.EncounterType.Should().Be(EncounterType.AnnualWellness);
    }

    [Fact]
    public void RecordVitals_WithAllValues_ShouldSetVitalSigns()
    {
        // Arrange
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);

        // Act
        encounter.RecordVitals(
            temperature: 37.0m,
            heartRate: 72,
            respiratoryRate: 16,
            systolicBp: 120,
            diastolicBp: 80,
            oxygenSaturation: 98.0m,
            height: 175.0m,
            weight: 70.0m,
            bmi: 22.9m);

        // Assert
        encounter.VitalSigns.Should().NotBeNull();
        encounter.VitalSigns!.Temperature.Should().Be(37.0m);
        encounter.VitalSigns.HeartRate.Should().Be(72);
        encounter.VitalSigns.RespiratoryRate.Should().Be(16);
        encounter.VitalSigns.SystolicBP.Should().Be(120);
        encounter.VitalSigns.DiastolicBP.Should().Be(80);
        encounter.VitalSigns.OxygenSaturation.Should().Be(98.0m);
        encounter.VitalSigns.HeightCm.Should().Be(175.0m);
        encounter.VitalSigns.WeightKg.Should().Be(70.0m);
        encounter.VitalSigns.Bmi.Should().Be(22.9m);
        encounter.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordVitals_WithNullValues_ShouldAllowNullableVitals()
    {
        // Arrange
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);

        // Act
        encounter.RecordVitals(
            temperature: null,
            heartRate: null,
            respiratoryRate: null,
            systolicBp: null,
            diastolicBp: null,
            oxygenSaturation: null,
            height: null,
            weight: null,
            bmi: null);

        // Assert
        encounter.VitalSigns.Should().NotBeNull();
        encounter.VitalSigns!.Temperature.Should().BeNull();
        encounter.VitalSigns.HeartRate.Should().BeNull();
        encounter.VitalSigns.RespiratoryRate.Should().BeNull();
    }

    [Fact]
    public void RecordHpi_WithAllValues_ShouldSetHpi()
    {
        // Arrange
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);

        // Act
        encounter.RecordHpi(
            onset: "2 days ago",
            location: "Left lower quadrant",
            duration: "Intermittent",
            characteristics: "Sharp, stabbing",
            aggravating: "Movement",
            relieving: "Rest",
            treatments: "Tylenol");

        // Assert
        encounter.Hpi.Should().NotBeNull();
        encounter.Hpi!.Onset.Should().Be("2 days ago");
        encounter.Hpi.Location.Should().Be("Left lower quadrant");
        encounter.Hpi.Duration.Should().Be("Intermittent");
        encounter.Hpi.Characteristics.Should().Be("Sharp, stabbing");
        encounter.Hpi.AggravatingFactors.Should().Be("Movement");
        encounter.Hpi.RelievingFactors.Should().Be("Rest");
        encounter.Hpi.PriorTreatments.Should().Be("Tylenol");
        encounter.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddDiagnosis_WithValidDiagnosis_ShouldAddToList()
    {
        // Arrange
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        var diagnosis = new Diagnosis("Hypertension", "I10", true, "Essential hypertension");

        // Act
        encounter.AddDiagnosis(diagnosis);

        // Assert
        encounter.Diagnoses.Should().HaveCount(1);
        encounter.Diagnoses.Should().Contain(diagnosis);
        encounter.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddDiagnosis_MultipleDiagnoses_ShouldContainAll()
    {
        // Arrange
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);
        var primary = new Diagnosis("Hypertension", "I10", true, "Essential hypertension");
        var secondary = new Diagnosis("Diabetes Type 2", "E11", false, null);

        // Act
        encounter.AddDiagnosis(primary);
        encounter.AddDiagnosis(secondary);

        // Assert
        encounter.Diagnoses.Should().HaveCount(2);
        encounter.Diagnoses.Should().ContainInOrder(primary, secondary);
    }

    [Fact]
    public void Complete_WithInProgressEncounter_ShouldSetStatusToCompleted()
    {
        // Arrange
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);

        // Act
        encounter.Complete();

        // Assert
        encounter.Status.Should().Be(EncounterStatus.Completed);
        encounter.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddDiagnosis_DiagnosisWithEmptyIcd10Code_ShouldThrow()
    {
        // Act
        var act = () => new Diagnosis("Test Condition", "", false, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("icd10Code");
    }

    [Fact]
    public void RecordAssessment_WithValidText_ShouldSetAssessment()
    {
        // Arrange
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);

        // Act
        encounter.RecordAssessment("Patient presents with acute chest pain likely due to GERD.");

        // Assert
        encounter.Assessment.Should().Be("Patient presents with acute chest pain likely due to GERD.");
        encounter.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordPlan_WithValidText_ShouldSetPlan()
    {
        // Arrange
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);

        // Act
        encounter.RecordPlan("Start omeprazole 20mg daily. Follow up in 2 weeks.");

        // Assert
        encounter.Plan.Should().Be("Start omeprazole 20mg daily. Follow up in 2 weeks.");
        encounter.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordHpi_WithNullValues_ShouldHandleNulls()
    {
        // Arrange
        var encounter = Encounter.Start(PatientId, ProviderId, EncounterType.Outpatient);

        // Act
        encounter.RecordHpi(null, null, null, null, null, null, null);

        // Assert
        encounter.Hpi.Should().NotBeNull();
        encounter.Hpi!.Onset.Should().BeNull();
        encounter.Hpi.Location.Should().BeNull();
    }

    [Fact]
    public void AddDiagnosis_DiagnosisWithEmptyConditionName_ShouldThrow()
    {
        // Act
        var act = () => new Diagnosis("", "I10", false, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("conditionName");
    }

    [Fact]
    public void Procedure_WithEmptyProcedureName_ShouldThrow()
    {
        // Act
        var act = () => new Procedure("", "99213", DateTime.UtcNow, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("procedureName");
    }

    [Fact]
    public void Procedure_WithEmptyCptCode_ShouldThrow()
    {
        // Act
        var act = () => new Procedure("Appendectomy", "", DateTime.UtcNow, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("cptCode");
    }
}
