using FluentAssertions;
using His.Hope.AppointmentService.Domain.ValueObjects;

namespace His.Hope.AppointmentService.Domain.Tests;

public class AppointmentValueObjectTests
{
    public class AppointmentIdTests
    {
        [Fact]
        public void Constructor_WithValidGuid_ShouldCreate()
        {
            var guid = Guid.NewGuid();
            var id = new AppointmentId(guid);
            id.Value.Should().Be(guid);
        }

        [Fact]
        public void Constructor_WithEmptyGuid_ShouldThrow()
        {
            var act = () => new AppointmentId(Guid.Empty);
            act.Should().Throw<ArgumentException>()
                .WithParameterName("value")
                .WithMessage("AppointmentId cannot be empty*");
        }

        [Fact]
        public void New_ShouldGenerateNonEmptyGuid()
        {
            var id = AppointmentId.New();
            id.Value.Should().NotBeEmpty();
        }

        [Fact]
        public void From_WithValidGuid_ShouldCreate()
        {
            var guid = Guid.NewGuid();
            var id = AppointmentId.From(guid);
            id.Value.Should().Be(guid);
        }

        [Fact]
        public void Equality_SameValue_ShouldBeEqual()
        {
            var guid = Guid.NewGuid();
            var id1 = new AppointmentId(guid);
            var id2 = new AppointmentId(guid);
            id1.Should().Be(id2);
            (id1 == id2).Should().BeTrue();
            id1.GetHashCode().Should().Be(id2.GetHashCode());
        }

        [Fact]
        public void Equality_DifferentValues_ShouldNotBeEqual()
        {
            var id1 = new AppointmentId(Guid.NewGuid());
            var id2 = new AppointmentId(Guid.NewGuid());
            id1.Should().NotBe(id2);
            (id1 != id2).Should().BeTrue();
        }

        [Fact]
        public void ToString_ShouldReturnGuidString()
        {
            var guid = Guid.NewGuid();
            var id = new AppointmentId(guid);
            id.ToString().Should().Be(guid.ToString());
        }
    }

    public class AppointmentStatusTests
    {
        [Fact]
        public void All_ShouldContainAllStatuses()
        {
            var all = AppointmentStatus.GetAll();
            all.Should().HaveCount(7);
            all.Should().Contain(AppointmentStatus.Scheduled);
            all.Should().Contain(AppointmentStatus.CheckedIn);
            all.Should().Contain(AppointmentStatus.InProgress);
            all.Should().Contain(AppointmentStatus.Completed);
            all.Should().Contain(AppointmentStatus.Cancelled);
            all.Should().Contain(AppointmentStatus.Rescheduled);
            all.Should().Contain(AppointmentStatus.NoShow);
        }

        [Theory]
        [InlineData("SCHEDULED", "Scheduled")]
        [InlineData("CHECKED_IN", "Checked In")]
        [InlineData("IN_PROGRESS", "In Progress")]
        [InlineData("COMPLETED", "Completed")]
        [InlineData("CANCELLED", "Cancelled")]
        [InlineData("RESCHEDULED", "Rescheduled")]
        [InlineData("NO_SHOW", "No Show")]
        public void FromCode_WithValidCode_ShouldReturnCorrect(string code, string expectedName)
        {
            var status = AppointmentStatus.FromCode(code);
            status.Code.Should().Be(code);
            status.Name.Should().Be(expectedName);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            var act = () => AppointmentStatus.FromCode("UNKNOWN");
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void FromName_WithValidName_ShouldReturnCorrect()
        {
            var status = AppointmentStatus.FromName("Cancelled");
            status.Should().Be(AppointmentStatus.Cancelled);
        }

        [Fact]
        public void Equality_SameStatus_ShouldBeEqual()
        {
            var s1 = AppointmentStatus.Scheduled;
            var s2 = AppointmentStatus.Scheduled;
            s1.Should().Be(s2);
        }

        [Fact]
        public void Equality_DifferentStatuses_ShouldNotBeEqual()
        {
            AppointmentStatus.Scheduled.Should().NotBe(AppointmentStatus.Completed);
        }

        [Fact]
        public void CompareTo_SameStatus_ShouldReturnZero()
        {
            AppointmentStatus.Scheduled.CompareTo(AppointmentStatus.Scheduled).Should().Be(0);
        }
    }

    public class AppointmentTypeTests
    {
        [Fact]
        public void All_ShouldContainAllTypes()
        {
            var all = AppointmentType.GetAll();
            all.Should().HaveCount(8);
            all.Should().Contain(AppointmentType.Checkup);
            all.Should().Contain(AppointmentType.Consultation);
            all.Should().Contain(AppointmentType.FollowUp);
            all.Should().Contain(AppointmentType.Emergency);
            all.Should().Contain(AppointmentType.Procedure);
            all.Should().Contain(AppointmentType.Vaccination);
            all.Should().Contain(AppointmentType.LabWork);
            all.Should().Contain(AppointmentType.Telehealth);
        }

        [Theory]
        [InlineData("CHECKUP", "General Checkup")]
        [InlineData("CONSULT", "Consultation")]
        [InlineData("FOLLOWUP", "Follow-up Visit")]
        [InlineData("EMERG", "Emergency Visit")]
        [InlineData("PROCED", "Procedure")]
        [InlineData("VACCINE", "Vaccination")]
        [InlineData("LAB", "Lab Work")]
        [InlineData("TELE", "Telehealth Visit")]
        public void FromCode_WithValidCode_ShouldReturnCorrect(string code, string expectedName)
        {
            var type = AppointmentType.FromCode(code);
            type.Code.Should().Be(code);
            type.Name.Should().Be(expectedName);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            var act = () => AppointmentType.FromCode("INVALID");
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Equality_SameType_ShouldBeEqual()
        {
            AppointmentType.Checkup.Should().Be(AppointmentType.Checkup);
        }

        [Fact]
        public void Equality_DifferentTypes_ShouldNotBeEqual()
        {
            AppointmentType.Checkup.Should().NotBe(AppointmentType.Emergency);
        }
    }
}
