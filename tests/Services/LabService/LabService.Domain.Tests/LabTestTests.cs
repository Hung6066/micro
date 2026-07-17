using FluentAssertions;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.LabService.Domain.Tests;

public class LabTestTests
{
    private static readonly LabOrderId DefaultOrderId = LabOrderId.New();
    private const string DefaultTestCode = "CBC";
    private const string DefaultTestName = "Complete Blood Count";
    private const string DefaultSpecimenType = "Blood";

    private LabTest CreateDefaultTest()
    {
        return LabTest.Create(DefaultOrderId, DefaultTestCode, DefaultTestName, DefaultSpecimenType);
    }

    [Fact]
    public void Create_WithValidParameters_ShouldCreateOrderedTest()
    {
        var test = CreateDefaultTest();

        test.Should().NotBeNull();
        test.LabOrderId.Should().Be(DefaultOrderId);
        test.TestCode.Should().Be(DefaultTestCode);
        test.TestName.Should().Be(DefaultTestName);
        test.SpecimenType.Should().Be(DefaultSpecimenType);
        test.Status.Should().Be(LabTestStatus.Ordered);
        test.Result.Should().BeNull();
        test.CollectedAt.Should().BeNull();
        test.CompletedAt.Should().BeNull();
        test.OrderedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        test.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNullSpecimenType_ShouldCreateTest()
    {
        var test = LabTest.Create(DefaultOrderId, DefaultTestCode, DefaultTestName, null);

        test.SpecimenType.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyTestCode_ShouldThrow()
    {
        var act = () => LabTest.Create(DefaultOrderId, "", DefaultTestName, null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("testCode");
    }

    [Fact]
    public void Create_WithEmptyTestName_ShouldThrow()
    {
        var act = () => LabTest.Create(DefaultOrderId, DefaultTestCode, "", null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("testName");
    }

    [Fact]
    public void MarkCollected_WithOrderedTest_ShouldTransitionToCollected()
    {
        var test = CreateDefaultTest();

        test.MarkCollected();

        test.Status.Should().Be(LabTestStatus.Collected);
        test.CollectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        test.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkCollected_WithExplicitDateTime_ShouldSetCollectedAt()
    {
        var test = CreateDefaultTest();
        var collectedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        test.MarkCollected(collectedAt);

        test.CollectedAt.Should().Be(collectedAt);
    }

    [Fact]
    public void MarkCollected_WhenAlreadyCollected_ShouldThrow()
    {
        var test = CreateDefaultTest();
        test.MarkCollected();

        var act = () => test.MarkCollected();

        act.Should().Throw<DomainException>()
            .WithMessage("Lab test has already been collected.");
    }

    [Fact]
    public void MarkCollected_WhenCancelled_ShouldThrow()
    {
        var test = CreateDefaultTest();
        test.Cancel();

        var act = () => test.MarkCollected();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot collect a cancelled lab test.");
    }

    [Fact]
    public void MarkCollected_WhenResulted_ShouldThrow()
    {
        var test = CreateDefaultTest();
        test.MarkCollected();
        test.MarkInProgress();
        var result = new LabResult(LabResultId.New(), "5.5", "x10^9/L", null,
            AbnormalFlag.Normal, LabResultStatus.Final, "Dr. Smith", null);
        test.RecordResult(result);

        var act = () => test.MarkCollected();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot collect a resulted lab test.");
    }

    [Fact]
    public void MarkInProgress_WithCollectedTest_ShouldTransitionToInProgress()
    {
        var test = CreateDefaultTest();
        test.MarkCollected();

        test.MarkInProgress();

        test.Status.Should().Be(LabTestStatus.InProgress);
        test.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkInProgress_WhenAlreadyInProgress_ShouldThrow()
    {
        var test = CreateDefaultTest();
        test.MarkCollected();
        test.MarkInProgress();

        var act = () => test.MarkInProgress();

        act.Should().Throw<DomainException>()
            .WithMessage("Lab test is already in progress.");
    }

    [Fact]
    public void MarkInProgress_WhenCancelled_ShouldThrow()
    {
        var test = CreateDefaultTest();
        test.Cancel();

        var act = () => test.MarkInProgress();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot start a cancelled lab test.");
    }

    [Fact]
    public void MarkInProgress_WhenResulted_ShouldThrow()
    {
        var test = CreateDefaultTest();
        test.MarkCollected();
        test.MarkInProgress();
        var result = new LabResult(LabResultId.New(), "5.5", "x10^9/L", null,
            AbnormalFlag.Normal, LabResultStatus.Final, "Dr. Smith", null);
        test.RecordResult(result);

        var act = () => test.MarkInProgress();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot start a resulted lab test.");
    }

    [Fact]
    public void RecordResult_WithInProgressTest_ShouldTransitionToResulted()
    {
        var test = CreateDefaultTest();
        test.MarkCollected();
        test.MarkInProgress();
        var result = new LabResult(LabResultId.New(), "5.5", "x10^9/L", "4.0-11.0",
            AbnormalFlag.Normal, LabResultStatus.Final, "Dr. Smith", null);

        test.RecordResult(result);

        test.Status.Should().Be(LabTestStatus.Resulted);
        test.Result.Should().Be(result);
        test.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        test.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordResult_WhenCancelled_ShouldThrow()
    {
        var test = CreateDefaultTest();
        test.Cancel();
        var result = new LabResult(LabResultId.New(), "5.5", "x10^9/L", null,
            AbnormalFlag.Normal, LabResultStatus.Final, "Dr. Smith", null);

        var act = () => test.RecordResult(result);

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot record result for a cancelled lab test.");
    }

    [Fact]
    public void RecordResult_WhenOrdered_ShouldThrow()
    {
        var test = CreateDefaultTest();
        var result = new LabResult(LabResultId.New(), "5.5", "x10^9/L", null,
            AbnormalFlag.Normal, LabResultStatus.Final, "Dr. Smith", null);

        var act = () => test.RecordResult(result);

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot record result before collecting sample.");
    }

    [Fact]
    public void RecordResult_WithNullResult_ShouldThrow()
    {
        var test = CreateDefaultTest();
        test.MarkCollected();
        test.MarkInProgress();

        var act = () => test.RecordResult(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("result");
    }

    [Fact]
    public void Cancel_WithOrderedTest_ShouldTransitionToCancelled()
    {
        var test = CreateDefaultTest();

        test.Cancel();

        test.Status.Should().Be(LabTestStatus.Cancelled);
        test.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldThrow()
    {
        var test = CreateDefaultTest();
        test.Cancel();

        var act = () => test.Cancel();

        act.Should().Throw<DomainException>()
            .WithMessage("Lab test has already been cancelled.");
    }

    [Fact]
    public void Cancel_WhenResulted_ShouldThrow()
    {
        var test = CreateDefaultTest();
        test.MarkCollected();
        test.MarkInProgress();
        var result = new LabResult(LabResultId.New(), "5.5", "x10^9/L", null,
            AbnormalFlag.Normal, LabResultStatus.Final, "Dr. Smith", null);
        test.RecordResult(result);

        var act = () => test.Cancel();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot cancel a resulted lab test.");
    }
}
