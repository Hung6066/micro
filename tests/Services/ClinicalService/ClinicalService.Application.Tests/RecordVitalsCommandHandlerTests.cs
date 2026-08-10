using AutoMapper;
using FluentAssertions;
using His.Hope.ClinicalService.Application.Common.Exceptions;
using His.Hope.ClinicalService.Application.DTOs;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;

using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.ClinicalService.Application.Tests;

public class RecordVitalsCommandHandlerTests
{
    private readonly Mock<IEncounterRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly RecordVitalsCommandHandler _handler;

    public RecordVitalsCommandHandlerTests()
    {
        _mockRepository = new Mock<IEncounterRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);
        _handler = new RecordVitalsCommandHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithExistingEncounter_ShouldRecordVitalsAndReturnDto()
    {
        var encounterId = Guid.NewGuid();
        var encounter = Encounter.Start(Guid.NewGuid(), Guid.NewGuid(), EncounterType.Outpatient);
        var command = new RecordVitalsCommand(
            encounterId, 37.0m, 72, 16, 120, 80, 98.0m, 175.0m, 70.0m, 22.9m);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(encounter);

        var expectedDto = new EncounterDto { Id = encounterId };
        _mockMapper.Setup(m => m.Map<EncounterDto>(encounter)).Returns(expectedDto);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(expectedDto);
        encounter.VitalSigns.Should().NotBeNull();
        encounter.VitalSigns!.Temperature.Should().Be(37.0m);
        encounter.VitalSigns.HeartRate.Should().Be(72);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingEncounter_ShouldThrowNotFoundException()
    {
        var command = new RecordVitalsCommand(Guid.NewGuid(), null, null, null, null, null, null, null, null, null);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Encounter?)null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullVitals_ShouldSetNullVitals()
    {
        var encounter = Encounter.Start(Guid.NewGuid(), Guid.NewGuid(), EncounterType.Outpatient);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(encounter);
        _mockMapper.Setup(m => m.Map<EncounterDto>(It.IsAny<Encounter>())).Returns(new EncounterDto());

        await _handler.Handle(
            new RecordVitalsCommand(Guid.NewGuid(), null, null, null, null, null, null, null, null, null),
            CancellationToken.None);

        encounter.VitalSigns!.Temperature.Should().BeNull();
        encounter.VitalSigns.HeartRate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldRaiseVitalsRecordedEvent()
    {
        var encounter = Encounter.Start(Guid.NewGuid(), Guid.NewGuid(), EncounterType.Outpatient);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(encounter);
        _mockMapper.Setup(m => m.Map<EncounterDto>(It.IsAny<Encounter>())).Returns(new EncounterDto());

        encounter.ClearDomainEvents();
        await _handler.Handle(
            new RecordVitalsCommand(Guid.NewGuid(), 37.0m, 72, 16, 120, 80, 98.0m, null, null, null),
            CancellationToken.None);

        encounter.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<His.Hope.ClinicalService.Domain.Events.VitalsRecordedDomainEvent>();
    }
}
