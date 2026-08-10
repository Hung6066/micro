using FluentAssertions;
using His.Hope.PatientService.Domain.ValueObjects;

namespace His.Hope.PatientService.Domain.Tests;

public class PatientValueObjectTests
{
    public class BloodTypeTests
    {
        [Fact]
        public void All_ShouldContainAllBloodTypes()
        {
            // Act
            var all = BloodType.GetAll();

            // Assert
            all.Should().HaveCount(9);
            all.Should().Contain(BloodType.APositive);
            all.Should().Contain(BloodType.ANegative);
            all.Should().Contain(BloodType.BPositive);
            all.Should().Contain(BloodType.BNegative);
            all.Should().Contain(BloodType.ABPositive);
            all.Should().Contain(BloodType.ABNegative);
            all.Should().Contain(BloodType.OPositive);
            all.Should().Contain(BloodType.ONegative);
            all.Should().Contain(BloodType.Unknown);
        }

        [Theory]
        [InlineData("A+", "A Positive")]
        [InlineData("A-", "A Negative")]
        [InlineData("B+", "B Positive")]
        [InlineData("B-", "B Negative")]
        [InlineData("AB+", "AB Positive")]
        [InlineData("AB-", "AB Negative")]
        [InlineData("O+", "O Positive")]
        [InlineData("O-", "O Negative")]
        [InlineData("U", "Unknown")]
        public void FromCode_WithValidCode_ShouldReturnCorrectType(string code, string expectedName)
        {
            // Act
            var bloodType = BloodType.FromCode(code);

            // Assert
            bloodType.Code.Should().Be(code);
            bloodType.Name.Should().Be(expectedName);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            // Act
            var act = () => BloodType.FromCode("INVALID");

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Equality_SameType_ShouldBeEqual()
        {
            // Arrange
            var bt1 = BloodType.APositive;
            var bt2 = BloodType.APositive;

            // Act & Assert
            bt1.Should().Be(bt2);
            bt1.GetHashCode().Should().Be(bt2.GetHashCode());
        }

        [Fact]
        public void Equality_DifferentTypes_ShouldNotBeEqual()
        {
            // Arrange
            var bt1 = BloodType.APositive;
            var bt2 = BloodType.BNegative;

            // Act & Assert
            bt1.Should().NotBe(bt2);
        }
    }

    public class GenderTests
    {
        [Fact]
        public void All_ShouldContainAllGenders()
        {
            // Act
            var all = Gender.GetAll();

            // Assert
            all.Should().HaveCount(4);
            all.Should().Contain(Gender.Male);
            all.Should().Contain(Gender.Female);
            all.Should().Contain(Gender.Other);
            all.Should().Contain(Gender.Unknown);
        }

        [Theory]
        [InlineData("M", "Male")]
        [InlineData("F", "Female")]
        [InlineData("O", "Other")]
        [InlineData("U", "Unknown")]
        public void FromCode_WithValidCode_ShouldReturnCorrectGender(string code, string expectedName)
        {
            // Act
            var gender = Gender.FromCode(code);

            // Assert
            gender.Code.Should().Be(code);
            gender.Name.Should().Be(expectedName);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            // Act
            var act = () => Gender.FromCode("X");

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Equality_SameGender_ShouldBeEqual()
        {
            // Arrange
            var g1 = Gender.Male;
            var g2 = Gender.Male;

            // Act & Assert
            g1.Should().Be(g2);
        }
    }

    public class MaritalStatusTests
    {
        [Fact]
        public void All_ShouldContainAllStatuses()
        {
            // Act
            var all = MaritalStatus.GetAll();

            // Assert
            all.Should().HaveCount(5);
            all.Should().Contain(MaritalStatus.Single);
            all.Should().Contain(MaritalStatus.Married);
            all.Should().Contain(MaritalStatus.Divorced);
            all.Should().Contain(MaritalStatus.Widowed);
            all.Should().Contain(MaritalStatus.Unknown);
        }

        [Theory]
        [InlineData("S", "Single")]
        [InlineData("M", "Married")]
        [InlineData("D", "Divorced")]
        [InlineData("W", "Widowed")]
        [InlineData("U", "Unknown")]
        public void FromCode_WithValidCode_ShouldReturnCorrectStatus(string code, string expectedName)
        {
            // Act
            var status = MaritalStatus.FromCode(code);

            // Assert
            status.Code.Should().Be(code);
            status.Name.Should().Be(expectedName);
        }

        [Fact]
        public void Equality_SameStatus_ShouldBeEqual()
        {
            var s1 = MaritalStatus.Married;
            var s2 = MaritalStatus.Married;

            s1.Should().Be(s2);
        }
    }

    public class RaceTests
    {
        [Fact]
        public void All_ShouldContainAllRaces()
        {
            // Act
            var all = Race.GetAll();

            // Assert
            all.Should().HaveCount(6);
            all.Should().Contain(Race.Asian);
            all.Should().Contain(Race.Black);
            all.Should().Contain(Race.Hispanic);
            all.Should().Contain(Race.White);
            all.Should().Contain(Race.Other);
            all.Should().Contain(Race.Unknown);
        }

        [Theory]
        [InlineData("ASIAN", "Asian")]
        [InlineData("BLACK", "Black or African American")]
        [InlineData("HISP", "Hispanic or Latino")]
        [InlineData("WHITE", "White")]
        [InlineData("OTHER", "Other")]
        [InlineData("UNK", "Unknown")]
        public void FromCode_WithValidCode_ShouldReturnCorrectRace(string code, string expectedName)
        {
            // Act
            var race = Race.FromCode(code);

            // Assert
            race.Code.Should().Be(code);
            race.Name.Should().Be(expectedName);
        }

        [Fact]
        public void Equality_SameRace_ShouldBeEqual()
        {
            var r1 = Race.Asian;
            var r2 = Race.Asian;

            r1.Should().Be(r2);
        }
    }
}
