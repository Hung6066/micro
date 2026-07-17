using FluentAssertions;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Events;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.LabService.Domain.Tests;

public class LabOrderTests
{
    private static readonly Guid DefaultPatientId = Guid.NewGuid();
    private static readonly Guid DefaultProviderId = Guid.NewGuid();
    private static readonly Guid? DefaultEncounterId = Guid.NewGuid();
    private static readonly LabOrderPriority DefaultPriority = LabOrderPriority.Routine;
    private const string DefaultNotes = "Routine blood work";

    private LabOrder CreateDefaultOrder()
    {
        return LabOrder.Create(DefaultPatientId, DefaultProviderId, DefaultEncounterId, DefaultPriority, DefaultNotes);
    }

    private LabTest CreateDefaultTest(LabOrder order)
    {
        return LabTest.Create(order.Id, "CBC", "Complete Blood Count", "Blood");
    }

    [Fact]
    public void Create_WithValidParameters_ShouldCreatePendingOrder()
    {
        var order = CreateDefaultOrder();

        order.Should().NotBeNull();
        order.PatientId.Should().Be(DefaultPatientId);
        order.ProviderId.Should().Be(DefaultProviderId);
        order.EncounterId.Should().Be(DefaultEncounterId);
        order.Priority.Should().Be(DefaultPriority);
        order.Notes.Should().Be(DefaultNotes);
        order.Status.Should().Be(LabOrderStatus.Pending);
        order.RequestedTests.Should().BeEmpty();
        order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        order.OrderDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNullEncounterId_ShouldCreateOrder()
    {
        var order = LabOrder.Create(DefaultPatientId, DefaultProviderId, null, DefaultPriority, null);

        order.Should().NotBeNull();
        order.EncounterId.Should().BeNull();
        order.Notes.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRaiseLabOrderCreatedDomainEvent()
    {
        var order = CreateDefaultOrder();

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LabOrderCreatedDomainEvent>()
            .Which.LabOrderId.Should().Be(order.Id.Value);
    }

    [Fact]
    public void AddTest_WithValidTest_ShouldAddToOrder()
    {
        var order = CreateDefaultOrder();
        var test = CreateDefaultTest(order);

        order.AddTest(test);

        order.RequestedTests.Should().HaveCount(1);
        order.RequestedTests.Should().Contain(test);
        order.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddTest_WithNullTest_ShouldThrow()
    {
        var order = CreateDefaultOrder();

        var act = () => order.AddTest(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("test");
    }

    [Fact]
    public void AddTest_OnSubmittedOrder_ShouldThrow()
    {
        var order = CreateDefaultOrder();
        var test = CreateDefaultTest(order);
        order.AddTest(test);
        order.ClearDomainEvents();
        order.Submit();

        var newTest = CreateDefaultTest(order);
        var act = () => order.AddTest(newTest);

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot add tests to a non-pending lab order.");
    }

    [Fact]
    public void AddTest_OnCancelledOrder_ShouldThrow()
    {
        var order = CreateDefaultOrder();
        order.ClearDomainEvents();
        order.Cancel("Test cancellation");

        var test = CreateDefaultTest(order);
        var act = () => order.AddTest(test);

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot add tests to a non-pending lab order.");
    }

    [Fact]
    public void Submit_WithValidOrder_ShouldTransitionToSubmitted()
    {
        var order = CreateDefaultOrder();
        var test = CreateDefaultTest(order);
        order.AddTest(test);
        order.ClearDomainEvents();

        order.Submit();

        order.Status.Should().Be(LabOrderStatus.Submitted);
        order.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Submit_ShouldRaiseLabOrderSubmittedDomainEvent()
    {
        var order = CreateDefaultOrder();
        var test = CreateDefaultTest(order);
        order.AddTest(test);
        order.ClearDomainEvents();

        order.Submit();

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LabOrderSubmittedDomainEvent>()
            .Which.LabOrderId.Should().Be(order.Id.Value);
    }

    [Fact]
    public void Submit_WithNoTests_ShouldThrow()
    {
        var order = CreateDefaultOrder();
        order.ClearDomainEvents();

        var act = () => order.Submit();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot submit a lab order with no tests.");
    }

    [Fact]
    public void Submit_WhenAlreadySubmitted_ShouldThrow()
    {
        var order = CreateDefaultOrder();
        var test = CreateDefaultTest(order);
        order.AddTest(test);
        order.ClearDomainEvents();
        order.Submit();

        var act = () => order.Submit();

        act.Should().Throw<DomainException>()
            .WithMessage("Lab order has already been submitted.");
    }

    [Fact]
    public void Submit_WhenCancelled_ShouldThrow()
    {
        var order = CreateDefaultOrder();
        order.ClearDomainEvents();
        order.Cancel("Cancelled");

        var act = () => order.Submit();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot submit a cancelled lab order.");
    }

    [Fact]
    public void Cancel_WithPendingOrder_ShouldTransitionToCancelled()
    {
        var order = CreateDefaultOrder();
        var test = CreateDefaultTest(order);
        order.AddTest(test);
        order.ClearDomainEvents();

        order.Cancel("Patient request");

        order.Status.Should().Be(LabOrderStatus.Cancelled);
        order.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Cancel_ShouldRaiseLabOrderCancelledDomainEvent()
    {
        var order = CreateDefaultOrder();
        order.ClearDomainEvents();

        order.Cancel("Patient request");

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LabOrderCancelledDomainEvent>()
            .Which.LabOrderId.Should().Be(order.Id.Value);
    }

    [Fact]
    public void Cancel_ShouldIncludeReasonInEvent()
    {
        var order = CreateDefaultOrder();
        order.ClearDomainEvents();

        order.Cancel("Patient request");

        var domainEvent = order.DomainEvents.OfType<LabOrderCancelledDomainEvent>().Single();
        domainEvent.Reason.Should().Be("Patient request");
    }

    [Fact]
    public void Cancel_WithEmptyReason_ShouldKeepOriginalNotes()
    {
        var order = CreateDefaultOrder();
        order.ClearDomainEvents();

        order.Cancel("");

        order.Notes.Should().Be(DefaultNotes);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldThrow()
    {
        var order = CreateDefaultOrder();
        order.ClearDomainEvents();
        order.Cancel("First cancellation");

        var act = () => order.Cancel("Second cancellation");

        act.Should().Throw<DomainException>()
            .WithMessage("Lab order has already been cancelled.");
    }

    [Fact]
    public void Cancel_ShouldCancelAllNonResultedTests()
    {
        var order = CreateDefaultOrder();
        var test1 = CreateDefaultTest(order);
        var test2 = LabTest.Create(order.Id, "BMP", "Basic Metabolic Panel", "Blood");
        order.AddTest(test1);
        order.AddTest(test2);
        order.ClearDomainEvents();

        test2.MarkCollected();
        test2.MarkInProgress();
        order.ClearDomainEvents();

        order.Cancel("Cancelling");

        test1.Status.Should().Be(LabTestStatus.Cancelled);
        test2.Status.Should().Be(LabTestStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldNotCancelResultedTests()
    {
        var order = CreateDefaultOrder();
        var test = CreateDefaultTest(order);
        order.AddTest(test);
        test.MarkCollected();
        test.MarkInProgress();
        var result = new LabResult(LabResultId.New(), "5.5", "x10^9/L", "4.0-11.0",
            AbnormalFlag.Normal, LabResultStatus.Final, "Dr. Smith", null);
        test.RecordResult(result);
        order.ClearDomainEvents();

        order.Cancel("Cancelling");

        test.Status.Should().Be(LabTestStatus.Resulted);
    }
}
