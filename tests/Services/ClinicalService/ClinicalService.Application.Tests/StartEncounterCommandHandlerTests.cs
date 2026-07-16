using AutoMapper;
using FluentAssertions;
using His.Hope.ClinicalService.Application.DTOs;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.ClinicalService.Application.Tests;

public class StartEncounterCommandHandlerTests
{
    private readonly Mock<IEncounterRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly StartEncounterCommandHandler _handler;

    public StartEncounterCommandHandlerTests()
    {
        _mockRepository = new Mock<IEncounterRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new StartEncounterCommandHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateEncounterAndReturnDto()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: "OP",
            EncounterDate: null,
            ChiefComplaint: null);

        var expectedDto = new EncounterDto
        {
            Id = Guid.NewGuid(),
            PatientId = command.PatientId,
            ProviderId = command.ProviderId,
            EncounterTypeCode = "OP",
            StatusCode = "IN_PROGRESS"
        };

        _mockMapper.Setup(m => m.Map<EncounterDto>(It.IsAny<Encounter>()))
            .Returns(expectedDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedDto);
        result.PatientId.Should().Be(command.PatientId);
        result.ProviderId.Should().Be(command.ProviderId);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Encounter>(e =>
                e.PatientId == command.PatientId &&
                e.ProviderId == command.ProviderId &&
                e.EncounterType == EncounterType.Outpatient &&
                e.ChiefComplaint == null),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithChiefComplaint_ShouldSetChiefComplaint()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: "ER",
            EncounterDate: null,
            ChiefComplaint: "Chest pain");

        _mockMapper.Setup(m => m.Map<EncounterDto>(It.IsAny<Encounter>()))
            .Returns(new EncounterDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Encounter>(e => e.ChiefComplaint == "Chest pain"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyChiefComplaint_ShouldNotSetChiefComplaint()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: "TH",
            EncounterDate: null,
            ChiefComplaint: "");

        _mockMapper.Setup(m => m.Map<EncounterDto>(It.IsAny<Encounter>()))
            .Returns(new EncounterDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Encounter>(e => e.ChiefComplaint == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidEncounterTypeCode_ShouldThrow()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: "INVALID",
            EncounterDate: null,
            ChiefComplaint: null);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Encounter>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAllEncounterTypes_ShouldCreateCorrectType()
    {
        // Arrange
        var typeCodes = new[] { "OP", "IP", "ER", "TH", "FU", "AW" };
        _mockMapper.Setup(m => m.Map<EncounterDto>(It.IsAny<Encounter>()))
            .Returns(new EncounterDto());

        foreach (var typeCode in typeCodes)
        {
            var command = new StartEncounterCommand(
                PatientId: Guid.NewGuid(),
                ProviderId: Guid.NewGuid(),
                AppointmentId: null,
                EncounterTypeCode: typeCode,
                EncounterDate: null,
                ChiefComplaint: null);

            // Act
            await _handler.Handle(command, CancellationToken.None);
        }

        // Assert
        _mockRepository.Verify(r => r.AddAsync(
            It.IsAny<Encounter>(), It.IsAny<CancellationToken>()),
            Times.Exactly(6));
    }
}
