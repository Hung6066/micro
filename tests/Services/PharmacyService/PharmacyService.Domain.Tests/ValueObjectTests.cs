using FluentAssertions;
using His.Hope.PharmacyService.Domain.ValueObjects;

namespace His.Hope.PharmacyService.Domain.Tests;

public class ValueObjectTests
{
    public class PrescriptionIdTests
    {
        [Fact]
        public void SameValues_ShouldBeEqual()
        {
            var guid = Guid.NewGuid();
            var id1 = PrescriptionId.From(guid);
            var id2 = PrescriptionId.From(guid);

            id1.Should().Be(id2);
            (id1 == id2).Should().BeTrue();
            id1.GetHashCode().Should().Be(id2.GetHashCode());
        }

        [Fact]
        public void DifferentValues_ShouldNotBeEqual()
        {
            var id1 = PrescriptionId.From(Guid.NewGuid());
            var id2 = PrescriptionId.From(Guid.NewGuid());

            id1.Should().NotBe(id2);
            (id1 != id2).Should().BeTrue();
        }

        [Fact]
        public void Create_WithEmptyGuid_ShouldThrow()
        {
            var act = () => new PrescriptionId(Guid.Empty);

            act.Should().Throw<ArgumentException>()
                .WithParameterName("value");
        }

        [Fact]
        public void New_ShouldGenerateNonEmptyId()
        {
            var id = PrescriptionId.New();

            id.Value.Should().NotBeEmpty();
        }

        [Fact]
        public void ToString_ShouldReturnGuidString()
        {
            var guid = Guid.NewGuid();
            var id = PrescriptionId.From(guid);

            id.ToString().Should().Be(guid.ToString());
        }
    }

    public class MedicationIdTests
    {
        [Fact]
        public void SameValues_ShouldBeEqual()
        {
            var guid = Guid.NewGuid();
            var id1 = MedicationId.From(guid);
            var id2 = MedicationId.From(guid);

            id1.Should().Be(id2);
            (id1 == id2).Should().BeTrue();
            id1.GetHashCode().Should().Be(id2.GetHashCode());
        }

        [Fact]
        public void DifferentValues_ShouldNotBeEqual()
        {
            var id1 = MedicationId.From(Guid.NewGuid());
            var id2 = MedicationId.From(Guid.NewGuid());

            id1.Should().NotBe(id2);
            (id1 != id2).Should().BeTrue();
        }

        [Fact]
        public void Create_WithEmptyGuid_ShouldThrow()
        {
            var act = () => new MedicationId(Guid.Empty);

            act.Should().Throw<ArgumentException>()
                .WithParameterName("value");
        }

        [Fact]
        public void New_ShouldGenerateNonEmptyId()
        {
            var id = MedicationId.New();

            id.Value.Should().NotBeEmpty();
        }
    }

    public class PrescriptionStatusTests
    {
        [Fact]
        public void Prescribed_ShouldHaveCorrectCode()
        {
            PrescriptionStatus.Prescribed.Code.Should().Be("PRESCRIBED");
            PrescriptionStatus.Prescribed.Name.Should().Be("Prescribed");
        }

        [Fact]
        public void Filled_ShouldHaveCorrectCode()
        {
            PrescriptionStatus.Filled.Code.Should().Be("FILLED");
            PrescriptionStatus.Filled.Name.Should().Be("Filled");
        }

        [Fact]
        public void Cancelled_ShouldHaveCorrectCode()
        {
            PrescriptionStatus.Cancelled.Code.Should().Be("CANCELLED");
            PrescriptionStatus.Cancelled.Name.Should().Be("Cancelled");
        }

        [Fact]
        public void Expired_ShouldHaveCorrectCode()
        {
            PrescriptionStatus.Expired.Code.Should().Be("EXPIRED");
            PrescriptionStatus.Expired.Name.Should().Be("Expired");
        }

        [Fact]
        public void FromCode_WithValidCode_ShouldReturnCorrectStatus()
        {
            var status = PrescriptionStatus.FromCode("FILLED");

            status.Should().Be(PrescriptionStatus.Filled);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            var act = () => PrescriptionStatus.FromCode("INVALID");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*INVALID*");
        }

        [Fact]
        public void SameCode_ShouldBeEqual()
        {
            var s1 = PrescriptionStatus.FromCode("PRESCRIBED");
            var s2 = PrescriptionStatus.Prescribed;

            s1.Should().Be(s2);
            s1.GetHashCode().Should().Be(s2.GetHashCode());
        }
    }
}
