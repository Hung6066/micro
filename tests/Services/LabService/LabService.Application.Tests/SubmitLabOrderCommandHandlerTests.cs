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

public class SubmitLabOrderCommandHandlerTests
{
    private readonly Mock<ILabOrderRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly SubmitLabOrderCommandHandler _handler;

    public SubmitLabOrderCommandHandlerTests()
    {
        _mockRepository = new Mock<ILabOrderRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new SubmitLabOrderCommandHandler(_mockRepository.Object);
    }

    private LabOrder CreateOrderWithTest()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        var test = LabTest.Create(order.Id, "CBC", "Complete Blood Count", "Blood");
        order.AddTest(test);
        return order;
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldSubmitOrder()
    {
        var order = CreateOrderWithTest();
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new SubmitLabOrderCommand(order.Id.Value);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        order.Status.Should().Be(LabOrderStatus.Submitted);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldThrow()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabOrder?)null);

        var command = new SubmitLabOrderCommand(Guid.NewGuid());

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*LabOrder*");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
