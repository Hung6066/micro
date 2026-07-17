using AutoMapper;
using FluentAssertions;
using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Application.UseCases.LabOrders.Commands;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.LabService.Application.Tests;

public class CreateLabOrderCommandHandlerTests
{
    private readonly Mock<ILabOrderRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<DomainEventDispatcher> _mockEventDispatcher;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreateLabOrderCommandHandler _handler;

    public CreateLabOrderCommandHandlerTests()
    {
        _mockRepository = new Mock<ILabOrderRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockEventDispatcher = new Mock<DomainEventDispatcher>(Mock.Of<MediatR.IMediator>());
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new CreateLabOrderCommandHandler(
            _mockRepository.Object,
            _mockMapper.Object,
            _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateOrderAndReturnDto()
    {
        var patientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var command = new CreateLabOrderCommand(
            patientId,
            providerId,
            null,
            "ROUTINE",
            "Routine blood work",
            new List<TestItem>
            {
                new("CBC", "Complete Blood Count", "Blood"),
                new("BMP", "Basic Metabolic Panel", "Blood")
            });

        var expectedDto = new LabOrderDto
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            ProviderId = providerId
        };

        _mockMapper.Setup(m => m.Map<LabOrderDto>(It.IsAny<LabOrder>()))
            .Returns(expectedDto);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<LabOrder>(o =>
                o.PatientId == patientId &&
                o.ProviderId == providerId &&
                o.Priority == LabOrderPriority.Routine &&
                o.Notes == "Routine blood work" &&
                o.RequestedTests.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEncounterId_ShouldSetEncounterId()
    {
        var encounterId = Guid.NewGuid();
        var command = new CreateLabOrderCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            encounterId,
            "URGENT",
            null,
            new List<TestItem> { new("CBC", "Complete Blood Count", null) });

        _mockMapper.Setup(m => m.Map<LabOrderDto>(It.IsAny<LabOrder>()))
            .Returns(new LabOrderDto());

        await _handler.Handle(command, CancellationToken.None);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<LabOrder>(o => o.EncounterId == encounterId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithUrgentPriority_ShouldSetPriority()
    {
        var command = new CreateLabOrderCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "URGENT",
            null,
            new List<TestItem> { new("CBC", "Complete Blood Count", null) });

        _mockMapper.Setup(m => m.Map<LabOrderDto>(It.IsAny<LabOrder>()))
            .Returns(new LabOrderDto());

        await _handler.Handle(command, CancellationToken.None);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<LabOrder>(o => o.Priority == LabOrderPriority.Urgent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidPriorityCode_ShouldThrow()
    {
        var command = new CreateLabOrderCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "INVALID",
            null,
            new List<TestItem> { new("CBC", "Complete Blood Count", null) });

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<LabOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldMapToDtoCorrectly()
    {
        var command = new CreateLabOrderCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "ROUTINE",
            null,
            new List<TestItem> { new("CBC", "Complete Blood Count", null) });

        _mockMapper.Setup(m => m.Map<LabOrderDto>(It.IsAny<LabOrder>()))
            .Returns(new LabOrderDto());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        _mockMapper.Verify(m => m.Map<LabOrderDto>(It.Is<LabOrder>(o =>
            o.Priority == LabOrderPriority.Routine)), Times.Once);
    }
}
