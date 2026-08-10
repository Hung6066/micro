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

public class AddLabTestCommandHandlerTests
{
    private readonly Mock<ILabOrderRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly AddLabTestCommandHandler _handler;

    public AddLabTestCommandHandlerTests()
    {
        _mockRepository = new Mock<ILabOrderRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new AddLabTestCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldAddTestToOrder()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        var orderId = order.Id.Value;
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new AddLabTestCommand(orderId, "CBC", "Complete Blood Count", "Blood");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        order.RequestedTests.Should().HaveCount(1);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithSpecimenType_ShouldSetSpecimenType()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new AddLabTestCommand(order.Id.Value, "CBC", "Complete Blood Count", "Serum");

        await _handler.Handle(command, CancellationToken.None);

        order.RequestedTests.Single().SpecimenType.Should().Be("Serum");
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldThrow()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabOrder?)null);

        var command = new AddLabTestCommand(Guid.NewGuid(), "CBC", "Complete Blood Count", "Blood");

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*LabOrder*");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
