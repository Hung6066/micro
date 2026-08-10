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

public class CollectLabTestCommandHandlerTests
{
    private readonly Mock<ILabOrderRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CollectLabTestCommandHandler _handler;

    public CollectLabTestCommandHandlerTests()
    {
        _mockRepository = new Mock<ILabOrderRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new CollectLabTestCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldMarkTestCollected()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        var test = LabTest.Create(order.Id, "CBC", "Complete Blood Count", "Blood");
        order.AddTest(test);
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new CollectLabTestCommand(order.Id.Value, test.Id.Value, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        test.Status.Should().Be(LabTestStatus.Collected);
        test.CollectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExplicitCollectionTime_ShouldSetCollectedAt()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        var test = LabTest.Create(order.Id, "CBC", "Complete Blood Count", "Blood");
        order.AddTest(test);
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var collectedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var command = new CollectLabTestCommand(order.Id.Value, test.Id.Value, collectedAt);

        await _handler.Handle(command, CancellationToken.None);

        test.CollectedAt.Should().Be(collectedAt);
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldThrow()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabOrder?)null);

        var command = new CollectLabTestCommand(Guid.NewGuid(), Guid.NewGuid(), null);

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

        var command = new CollectLabTestCommand(order.Id.Value, Guid.NewGuid(), null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*LabTest*");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
