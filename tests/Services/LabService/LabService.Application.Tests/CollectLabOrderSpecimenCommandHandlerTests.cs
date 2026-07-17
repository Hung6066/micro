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

public class CollectLabOrderSpecimenCommandHandlerTests
{
    private readonly Mock<ILabOrderRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CollectLabOrderSpecimenCommandHandler _handler;

    public CollectLabOrderSpecimenCommandHandlerTests()
    {
        _mockRepository = new Mock<ILabOrderRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new CollectLabOrderSpecimenCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCollectAllOrderedTests()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        var test1 = LabTest.Create(order.Id, "CBC", "Complete Blood Count", "Blood");
        var test2 = LabTest.Create(order.Id, "BMP", "Basic Metabolic Panel", "Blood");
        order.AddTest(test1);
        order.AddTest(test2);
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new CollectLabOrderSpecimenCommand(order.Id.Value);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        test1.Status.Should().Be(LabTestStatus.Collected);
        test2.Status.Should().Be(LabTestStatus.Collected);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExplicitTime_ShouldSetCollectionTime()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        var test = LabTest.Create(order.Id, "CBC", "Complete Blood Count", "Blood");
        order.AddTest(test);
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var collectedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var command = new CollectLabOrderSpecimenCommand(order.Id.Value, collectedAt);

        await _handler.Handle(command, CancellationToken.None);

        test.CollectedAt.Should().Be(collectedAt);
    }

    [Fact]
    public async Task Handle_ShouldNotCollectNonOrderedTests()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        var orderedTest = LabTest.Create(order.Id, "CBC", "Complete Blood Count", "Blood");
        var collectedTest = LabTest.Create(order.Id, "BMP", "Basic Metabolic Panel", "Blood");
        order.AddTest(orderedTest);
        order.AddTest(collectedTest);
        collectedTest.MarkCollected(); // already collected
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new CollectLabOrderSpecimenCommand(order.Id.Value);

        await _handler.Handle(command, CancellationToken.None);

        orderedTest.Status.Should().Be(LabTestStatus.Collected); // was collected now
        collectedTest.Status.Should().Be(LabTestStatus.Collected); // unchanged from already collected
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldThrow()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabOrder?)null);

        var command = new CollectLabOrderSpecimenCommand(Guid.NewGuid());

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*LabOrder*");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
