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

public class AddDiagnosisCommandHandlerTests
{
    private readonly Mock<IEncounterRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly AddDiagnosisCommandHandler _handler;

    public AddDiagnosisCommandHandlerTests()
    {
        _mockRepository = new Mock<IEncounterRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);
        _handler = new AddDiagnosisCommandHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithExistingEncounter_ShouldAddDiagnosisAndReturnDto()
    {
        var encounterId = Guid.NewGuid();
        var encounter = Encounter.Start(Guid.NewGuid(), Guid.NewGuid(), EncounterType.Outpatient);
        var command = new AddDiagnosisCommand(encounterId, "Hypertension", "I10", true, "Essential hypertension");

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(encounter);

        var expectedDto = new EncounterDto { Id = encounterId };
        _mockMapper.Setup(m => m.Map<EncounterDto>(encounter)).Returns(expectedDto);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(expectedDto);
        encounter.Diagnoses.Should().HaveCount(1);
        encounter.Diagnoses.First().ConditionName.Should().Be("Hypertension");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingEncounter_ShouldThrowNotFoundException()
    {
        var command = new AddDiagnosisCommand(Guid.NewGuid(), "Test", "A00", false, null);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Encounter?)null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithMultipleDiagnoses_ShouldAddAll()
    {
        var encounter = Encounter.Start(Guid.NewGuid(), Guid.NewGuid(), EncounterType.Outpatient);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(encounter);
        _mockMapper.Setup(m => m.Map<EncounterDto>(It.IsAny<Encounter>())).Returns(new EncounterDto());

        await _handler.Handle(new AddDiagnosisCommand(Guid.NewGuid(), "Primary", "A00", true, null), CancellationToken.None);
        await _handler.Handle(new AddDiagnosisCommand(Guid.NewGuid(), "Secondary", "B00", false, null), CancellationToken.None);

        encounter.Diagnoses.Should().HaveCount(2);
    }
}
