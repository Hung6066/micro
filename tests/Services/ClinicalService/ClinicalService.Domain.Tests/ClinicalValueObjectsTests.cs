using FluentAssertions;
using His.Hope.ClinicalService.Domain.ValueObjects;

namespace His.Hope.ClinicalService.Domain.Tests;

public class ClinicalValueObjectsTests
{
    public class EncounterIdTests
    {
        [Fact]
        public void Constructor_WithValidGuid_ShouldCreate()
        {
            // Arrange
            var guid = Guid.NewGuid();

            // Act
            var id = new EncounterId(guid);

            // Assert
            id.Value.Should().Be(guid);
        }

        [Fact]
        public void Constructor_WithEmptyGuid_ShouldThrow()
        {
            // Act
            var act = () => new EncounterId(Guid.Empty);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithParameterName("value")
                .WithMessage("EncounterId cannot be empty*");
        }

        [Fact]
        public void New_ShouldGenerateNonEmptyGuid()
        {
            // Act
            var id = EncounterId.New();

            // Assert
            id.Value.Should().NotBeEmpty();
        }

        [Fact]
        public void From_WithValidGuid_ShouldCreate()
        {
            // Arrange
            var guid = Guid.NewGuid();

            // Act
            var id = EncounterId.From(guid);

            // Assert
            id.Value.Should().Be(guid);
        }

        [Fact]
        public void Equality_SameValue_ShouldBeEqual()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var id1 = new EncounterId(guid);
            var id2 = new EncounterId(guid);

            // Act & Assert
            id1.Should().Be(id2);
            (id1 == id2).Should().BeTrue();
            id1.GetHashCode().Should().Be(id2.GetHashCode());
        }

        [Fact]
        public void Equality_DifferentValues_ShouldNotBeEqual()
        {
            // Arrange
            var id1 = new EncounterId(Guid.NewGuid());
            var id2 = new EncounterId(Guid.NewGuid());

            // Act & Assert
            id1.Should().NotBe(id2);
            (id1 != id2).Should().BeTrue();
        }

        [Fact]
        public void ToString_ShouldReturnGuidString()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var id = new EncounterId(guid);

            // Act
            var str = id.ToString();

            // Assert
            str.Should().Be(guid.ToString());
        }
    }

    public class VitalSignsTests
    {
        [Fact]
        public void Constructor_WithAllValues_ShouldSetProperties()
        {
            // Act
            var vitals = new VitalSigns(37.0m, 72, 16, 120, 80, 98.0m, 175.0m, 70.0m, 22.9m);

            // Assert
            vitals.Temperature.Should().Be(37.0m);
            vitals.HeartRate.Should().Be(72);
            vitals.RespiratoryRate.Should().Be(16);
            vitals.SystolicBP.Should().Be(120);
            vitals.DiastolicBP.Should().Be(80);
            vitals.OxygenSaturation.Should().Be(98.0m);
            vitals.HeightCm.Should().Be(175.0m);
            vitals.WeightKg.Should().Be(70.0m);
            vitals.Bmi.Should().Be(22.9m);
        }

        [Fact]
        public void Equality_SameValues_ShouldBeEqual()
        {
            // Arrange
            var v1 = new VitalSigns(37.0m, 72, 16, 120, 80, 98.0m, 175.0m, 70.0m, 22.9m);
            var v2 = new VitalSigns(37.0m, 72, 16, 120, 80, 98.0m, 175.0m, 70.0m, 22.9m);

            // Act & Assert
            v1.Should().Be(v2);
            v1.GetHashCode().Should().Be(v2.GetHashCode());
        }

        [Fact]
        public void Equality_DifferentValues_ShouldNotBeEqual()
        {
            // Arrange
            var v1 = new VitalSigns(37.0m, 72, 16, 120, 80, 98.0m, 175.0m, 70.0m, 22.9m);
            var v2 = new VitalSigns(38.0m, 72, 16, 120, 80, 98.0m, 175.0m, 70.0m, 22.9m);

            // Act & Assert
            v1.Should().NotBe(v2);
        }

        [Fact]
        public void Equality_NullValues_ShouldBeTreatedAsEqual()
        {
            // Arrange
            var v1 = new VitalSigns(null, null, null, null, null, null, null, null, null);
            var v2 = new VitalSigns(null, null, null, null, null, null, null, null, null);

            // Act & Assert
            v1.Should().Be(v2);
        }
    }

    public class DiagnosisTests
    {
        [Fact]
        public void Constructor_WithValidValues_ShouldSetProperties()
        {
            // Act
            var diagnosis = new Diagnosis("Hypertension", "I10", true, "Essential hypertension");

            // Assert
            diagnosis.ConditionName.Should().Be("Hypertension");
            diagnosis.Icd10Code.Should().Be("I10");
            diagnosis.IsPrimary.Should().BeTrue();
            diagnosis.Notes.Should().Be("Essential hypertension");
        }

        [Fact]
        public void Constructor_WithNullNotes_ShouldSetNull()
        {
            // Act
            var diagnosis = new Diagnosis("Hypertension", "I10", false, null);

            // Assert
            diagnosis.Notes.Should().BeNull();
        }

        [Fact]
        public void Equality_SameValues_ShouldBeEqual()
        {
            // Arrange
            var d1 = new Diagnosis("Hypertension", "I10", true, "Notes");
            var d2 = new Diagnosis("Hypertension", "I10", true, "Notes");

            // Act & Assert
            d1.Should().Be(d2);
        }

        [Fact]
        public void Equality_DifferentValues_ShouldNotBeEqual()
        {
            // Arrange
            var d1 = new Diagnosis("Hypertension", "I10", true, null);
            var d2 = new Diagnosis("Diabetes", "E11", false, null);

            // Act & Assert
            d1.Should().NotBe(d2);
        }
    }

    public class ProcedureTests
    {
        [Fact]
        public void Constructor_WithValidValues_ShouldSetProperties()
        {
            // Arrange
            var date = new DateTime(2024, 6, 15);

            // Act
            var procedure = new Procedure("Appendectomy", "44970", date, "Laparoscopic");

            // Assert
            procedure.ProcedureName.Should().Be("Appendectomy");
            procedure.CptCode.Should().Be("44970");
            procedure.PerformedDate.Should().Be(date);
            procedure.Notes.Should().Be("Laparoscopic");
        }

        [Fact]
        public void Equality_SameValues_ShouldBeEqual()
        {
            // Arrange
            var date = new DateTime(2024, 6, 15);
            var p1 = new Procedure("Appendectomy", "44970", date, null);
            var p2 = new Procedure("Appendectomy", "44970", date, null);

            // Act & Assert
            p1.Should().Be(p2);
        }
    }

    public class HistoryPresentIllnessTests
    {
        [Fact]
        public void Constructor_WithAllValues_ShouldSetProperties()
        {
            // Act
            var hpi = new HistoryPresentIllness("2 days ago", "Left lower quadrant", "Intermittent",
                "Sharp", "Movement", "Rest", "Tylenol");

            // Assert
            hpi.Onset.Should().Be("2 days ago");
            hpi.Location.Should().Be("Left lower quadrant");
            hpi.Duration.Should().Be("Intermittent");
            hpi.Characteristics.Should().Be("Sharp");
            hpi.AggravatingFactors.Should().Be("Movement");
            hpi.RelievingFactors.Should().Be("Rest");
            hpi.PriorTreatments.Should().Be("Tylenol");
        }

        [Fact]
        public void Equality_SameValues_ShouldBeEqual()
        {
            // Arrange
            var h1 = new HistoryPresentIllness("2 days", "LLQ", "Int", "Sharp", null, null, null);
            var h2 = new HistoryPresentIllness("2 days", "LLQ", "Int", "Sharp", null, null, null);

            // Act & Assert
            h1.Should().Be(h2);
        }
    }
}
