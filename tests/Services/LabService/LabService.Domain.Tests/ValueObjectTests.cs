using FluentAssertions;
using His.Hope.LabService.Domain.ValueObjects;

namespace His.Hope.LabService.Domain.Tests;

public class ValueObjectTests
{
    public class LabOrderStatusTests
    {
        [Fact]
        public void GetAll_ShouldReturnAllStatuses()
        {
            var all = LabOrderStatus.GetAll();

            all.Should().HaveCount(5);
            all.Should().Contain(LabOrderStatus.Pending);
            all.Should().Contain(LabOrderStatus.Submitted);
            all.Should().Contain(LabOrderStatus.InProgress);
            all.Should().Contain(LabOrderStatus.Completed);
            all.Should().Contain(LabOrderStatus.Cancelled);
        }

        [Fact]
        public void FromCode_WithValidCode_ShouldReturnCorrectStatus()
        {
            LabOrderStatus.FromCode("PENDING").Should().Be(LabOrderStatus.Pending);
            LabOrderStatus.FromCode("SUBMITTED").Should().Be(LabOrderStatus.Submitted);
            LabOrderStatus.FromCode("IN_PROGRESS").Should().Be(LabOrderStatus.InProgress);
            LabOrderStatus.FromCode("COMPLETED").Should().Be(LabOrderStatus.Completed);
            LabOrderStatus.FromCode("CANCELLED").Should().Be(LabOrderStatus.Cancelled);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            var act = () => LabOrderStatus.FromCode("INVALID");

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("'INVALID' is not a valid LabOrderStatus");
        }

        [Fact]
        public void FromCode_WithEmptyCode_ShouldThrow()
        {
            var act = () => LabOrderStatus.FromCode("");

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Status_ShouldHaveCorrectCodes()
        {
            LabOrderStatus.Pending.Code.Should().Be("PENDING");
            LabOrderStatus.Submitted.Code.Should().Be("SUBMITTED");
            LabOrderStatus.InProgress.Code.Should().Be("IN_PROGRESS");
            LabOrderStatus.Completed.Code.Should().Be("COMPLETED");
            LabOrderStatus.Cancelled.Code.Should().Be("CANCELLED");
        }

        [Fact]
        public void Status_ShouldHaveCorrectNames()
        {
            LabOrderStatus.Pending.Name.Should().Be("Pending");
            LabOrderStatus.Submitted.Name.Should().Be("Submitted");
            LabOrderStatus.InProgress.Name.Should().Be("In Progress");
            LabOrderStatus.Completed.Name.Should().Be("Completed");
            LabOrderStatus.Cancelled.Name.Should().Be("Cancelled");
        }
    }

    public class LabOrderPriorityTests
    {
        [Fact]
        public void GetAll_ShouldReturnAllPriorities()
        {
            var all = LabOrderPriority.GetAll();

            all.Should().HaveCount(4);
            all.Should().Contain(LabOrderPriority.Routine);
            all.Should().Contain(LabOrderPriority.Urgent);
            all.Should().Contain(LabOrderPriority.STAT);
            all.Should().Contain(LabOrderPriority.ASAP);
        }

        [Fact]
        public void FromCode_WithValidCode_ShouldReturnCorrectPriority()
        {
            LabOrderPriority.FromCode("ROUTINE").Should().Be(LabOrderPriority.Routine);
            LabOrderPriority.FromCode("URGENT").Should().Be(LabOrderPriority.Urgent);
            LabOrderPriority.FromCode("STAT").Should().Be(LabOrderPriority.STAT);
            LabOrderPriority.FromCode("ASAP").Should().Be(LabOrderPriority.ASAP);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            var act = () => LabOrderPriority.FromCode("INVALID");

            act.Should().Throw<InvalidOperationException>();
        }
    }

    public class LabTestStatusTests
    {
        [Fact]
        public void GetAll_ShouldReturnAllStatuses()
        {
            var all = LabTestStatus.GetAll();

            all.Should().HaveCount(5);
            all.Should().Contain(LabTestStatus.Ordered);
            all.Should().Contain(LabTestStatus.Collected);
            all.Should().Contain(LabTestStatus.InProgress);
            all.Should().Contain(LabTestStatus.Resulted);
            all.Should().Contain(LabTestStatus.Cancelled);
        }

        [Fact]
        public void FromCode_WithValidCode_ShouldReturnCorrectStatus()
        {
            LabTestStatus.FromCode("ORDERED").Should().Be(LabTestStatus.Ordered);
            LabTestStatus.FromCode("COLLECTED").Should().Be(LabTestStatus.Collected);
            LabTestStatus.FromCode("IN_PROGRESS").Should().Be(LabTestStatus.InProgress);
            LabTestStatus.FromCode("RESULTED").Should().Be(LabTestStatus.Resulted);
            LabTestStatus.FromCode("CANCELLED").Should().Be(LabTestStatus.Cancelled);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            var act = () => LabTestStatus.FromCode("INVALID");

            act.Should().Throw<InvalidOperationException>();
        }
    }

    public class LabResultStatusTests
    {
        [Fact]
        public void GetAll_ShouldReturnAllStatuses()
        {
            var all = LabResultStatus.GetAll();

            all.Should().HaveCount(4);
            all.Should().Contain(LabResultStatus.Pending);
            all.Should().Contain(LabResultStatus.Preliminary);
            all.Should().Contain(LabResultStatus.Final);
            all.Should().Contain(LabResultStatus.Corrected);
        }

        [Fact]
        public void FromCode_WithValidCode_ShouldReturnCorrectStatus()
        {
            LabResultStatus.FromCode("PENDING").Should().Be(LabResultStatus.Pending);
            LabResultStatus.FromCode("PRELIMINARY").Should().Be(LabResultStatus.Preliminary);
            LabResultStatus.FromCode("FINAL").Should().Be(LabResultStatus.Final);
            LabResultStatus.FromCode("CORRECTED").Should().Be(LabResultStatus.Corrected);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            var act = () => LabResultStatus.FromCode("INVALID");

            act.Should().Throw<InvalidOperationException>();
        }
    }

    public class AbnormalFlagTests
    {
        [Fact]
        public void GetAll_ShouldReturnAllFlags()
        {
            var all = AbnormalFlag.GetAll();

            all.Should().HaveCount(4);
            all.Should().Contain(AbnormalFlag.Normal);
            all.Should().Contain(AbnormalFlag.Abnormal);
            all.Should().Contain(AbnormalFlag.CriticalHigh);
            all.Should().Contain(AbnormalFlag.CriticalLow);
        }

        [Fact]
        public void FromCode_WithValidCode_ShouldReturnCorrectFlag()
        {
            AbnormalFlag.FromCode("NORMAL").Should().Be(AbnormalFlag.Normal);
            AbnormalFlag.FromCode("ABNORMAL").Should().Be(AbnormalFlag.Abnormal);
            AbnormalFlag.FromCode("CRITICAL_HIGH").Should().Be(AbnormalFlag.CriticalHigh);
            AbnormalFlag.FromCode("CRITICAL_LOW").Should().Be(AbnormalFlag.CriticalLow);
        }

        [Fact]
        public void FromCode_WithInvalidCode_ShouldThrow()
        {
            var act = () => AbnormalFlag.FromCode("INVALID");

            act.Should().Throw<InvalidOperationException>();
        }
    }
}
