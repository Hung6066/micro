using FluentAssertions;
using His.Hope.ClinicalService.Application.Common.Exceptions;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;

using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.ClinicalService.Application.Tests;

public class CompleteEncounterCommandHandlerTests
{
    private readonly Mock<IEncounterRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CompleteEncounterCommandHandler _handler;

    public CompleteEncounterCommandHandlerTests()
    {
        _mockRepository = new Mock<IEncounterRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);
        _handler = new CompleteEncounterCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithExistingEncounter_ShouldCompleteAndSave()
    {
        var encounterId = Guid.NewGuid();
        var encounter = Encounter.Start(Guid.NewGuid(), Guid.NewGuid(), EncounterType.Outpatient);
        var command = new CompleteEncounterCommand(encounterId);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(encounter);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        encounter.Status.Should().Be(EncounterStatus.Completed);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingEncounter_ShouldThrowNotFoundException()
    {
        var command = new CompleteEncounterCommand(Guid.NewGuid());

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Encounter?)null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRaiseCompletedDomainEvent()
    {
        var encounter = Encounter.Start(Guid.NewGuid(), Guid.NewGuid(), EncounterType.Outpatient);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(encounter);

        encounter.ClearDomainEvents();
        await _handler.Handle(new CompleteEncounterCommand(Guid.NewGuid()), CancellationToken.None);

        encounter.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<His.Hope.ClinicalService.Domain.Events.EncounterCompletedDomainEvent>();
    }
}
