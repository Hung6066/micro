using FluentAssertions;
using His.Hope.ClinicalService.Domain.ValueObjects;

namespace His.Hope.ClinicalService.Domain.Tests;

public class ClinicalEnumsTests
{
    public class EncounterTypeTests
    {
        [Fact]
        public void All_ShouldContainAllTypes()
        {
            // Act
            var all = EncounterType.GetAll();

            // Assert
            all.Should().HaveCount(6);
            all.Should().Contain(EncounterType.Outpatient);
            all.Should().Contain(EncounterType.Inpatient);
            all.Should().Contain(EncounterType.Emergency);
            all.Should().Contain(EncounterType.Telehealth);
            all.Should().Contain(EncounterType.FollowUp);
            all.Should().Contain(EncounterType.AnnualWellness);
        }

        [Theory]
        [InlineData("OP", "Outpatient")]
        [InlineData("IP", "Inpatient")]
        [InlineData("ER", "Emergency")]
        [InlineData("TH", "Telehealth")]
        [InlineData("FU", "Follow-up")]
        [InlineData("AW", "Annual Wellness")]
        public void FromCode_WithValidCode_ShouldReturnCorrectType(string code, string expectedName)
        {
            // Act
            var type = EncounterType.FromCode(code);

            // Assert
            type.Code.Should().Be(code);
            type.Name.Should().Be(expectedName);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            // Act
            var act = () => EncounterType.FromCode("INVALID");

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*INVALID*");
        }

        [Theory]
        [InlineData("Outpatient")]
        [InlineData("Inpatient")]
        [InlineData("Emergency")]
        [InlineData("Telehealth")]
        [InlineData("Follow-up")]
        [InlineData("Annual Wellness")]
        public void FromName_WithValidName_ShouldReturnCorrectType(string name)
        {
            // Act
            var type = EncounterType.FromName(name);

            // Assert
            type.Name.Should().Be(name);
        }

        [Fact]
        public void EncounterType_ImplementsIComparable()
        {
            // Arrange
            var op = EncounterType.Outpatient; // code = "OP"
            var er = EncounterType.Emergency; // code = "ER"

            // Act
            var result = op.CompareTo(er);

            // Assert
            // "OP" > "ER" alphabetically (O > E)
            result.Should().BePositive();
        }

        [Fact]
        public void EncounterType_SameType_Equals()
        {
            var op1 = EncounterType.Outpatient;
            var op2 = EncounterType.Outpatient;

            op1.Should().Be(op2);
            (op1 == op2).Should().BeTrue();
        }
    }

    public class EncounterStatusTests
    {
        [Fact]
        public void All_ShouldContainAllStatuses()
        {
            // Act
            var all = EncounterStatus.GetAll();

            // Assert
            all.Should().HaveCount(3);
            all.Should().Contain(EncounterStatus.InProgress);
            all.Should().Contain(EncounterStatus.Completed);
            all.Should().Contain(EncounterStatus.Signed);
        }

        [Theory]
        [InlineData("IN_PROGRESS", "In Progress")]
        [InlineData("COMPLETED", "Completed")]
        [InlineData("SIGNED", "Signed")]
        public void FromCode_WithValidCode_ShouldReturnCorrectStatus(string code, string expectedName)
        {
            // Act
            var status = EncounterStatus.FromCode(code);

            // Assert
            status.Code.Should().Be(code);
            status.Name.Should().Be(expectedName);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            // Act
            var act = () => EncounterStatus.FromCode("UNKNOWN");

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
    }
}
