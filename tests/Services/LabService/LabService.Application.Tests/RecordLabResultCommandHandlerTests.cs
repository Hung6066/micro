using FluentAssertions;
using His.Hope.LabService.Application.Common.Exceptions;
using His.Hope.LabService.Application.UseCases.LabOrders.Commands;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.LabService.Application.Tests;

public class RecordLabResultCommandHandlerTests
{
    private readonly Mock<ILabOrderRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly RecordLabResultCommandHandler _handler;

    public RecordLabResultCommandHandlerTests()
    {
        _mockRepository = new Mock<ILabOrderRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new RecordLabResultCommandHandler(_mockRepository.Object);
    }

    private (LabOrder order, LabTest test) CreateOrderWithCollectedTest()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        var test = LabTest.Create(order.Id, "CBC", "Complete Blood Count", "Blood");
        order.AddTest(test);
        test.MarkCollected();
        test.MarkInProgress();
        return (order, test);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldRecordResult()
    {
        var (order, test) = CreateOrderWithCollectedTest();
        var orderId = order.Id.Value;
        var testId = test.Id.Value;

        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new RecordLabResultCommand(
            orderId, testId, "5.5", "x10^9/L", "4.0-11.0",
            "NORMAL", "FINAL", "Dr. Smith", "All normal");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        test.Status.Should().Be(LabTestStatus.Resulted);
        test.Result.Should().NotBeNull();
        test.Result!.Value.Should().Be("5.5");
        test.Result.Unit.Should().Be("x10^9/L");
        test.Result.ReferenceRange.Should().Be("4.0-11.0");
        test.Result.AbnormalFlag.Should().Be(AbnormalFlag.Normal);
        test.Result.ResultStatus.Should().Be(LabResultStatus.Final);
        test.Result.PerformedBy.Should().Be("Dr. Smith");
        test.Result.Notes.Should().Be("All normal");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAbnormalFlag_ShouldSetAbnormalFlag()
    {
        var (order, test) = CreateOrderWithCollectedTest();
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new RecordLabResultCommand(
            order.Id.Value, test.Id.Value, "18.5", "x10^9/L", null,
            "CRITICAL_HIGH", "FINAL", null, null);

        await _handler.Handle(command, CancellationToken.None);

        test.Result!.AbnormalFlag.Should().Be(AbnormalFlag.CriticalHigh);
    }

    [Fact]
    public async Task Handle_WithPreliminaryResult_ShouldSetResultStatus()
    {
        var (order, test) = CreateOrderWithCollectedTest();
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new RecordLabResultCommand(
            order.Id.Value, test.Id.Value, "5.5", null, null,
            null, "PRELIMINARY", null, null);

        await _handler.Handle(command, CancellationToken.None);

        test.Result!.ResultStatus.Should().Be(LabResultStatus.Preliminary);
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldThrow()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabOrder?)null);

        var command = new RecordLabResultCommand(
            Guid.NewGuid(), Guid.NewGuid(), "5.5", null, null,
            null, "FINAL", null, null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*LabOrder*");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTestNotFound_ShouldThrow()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new RecordLabResultCommand(
            order.Id.Value, Guid.NewGuid(), "5.5", null, null,
            null, "FINAL", null, null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*LabTest*");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidResultStatusCode_ShouldThrow()
    {
        var (order, test) = CreateOrderWithCollectedTest();
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new RecordLabResultCommand(
            order.Id.Value, test.Id.Value, "5.5", null, null,
            null, "INVALID", null, null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
