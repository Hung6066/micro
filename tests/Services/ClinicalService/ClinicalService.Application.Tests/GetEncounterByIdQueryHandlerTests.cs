using AutoMapper;
using FluentAssertions;
using His.Hope.ClinicalService.Application.DTOs;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Queries;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;
using Moq;

namespace His.Hope.ClinicalService.Application.Tests;

public class GetEncounterByIdQueryHandlerTests
{
    private readonly Mock<IEncounterRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly GetEncounterByIdQueryHandler _handler;

    public GetEncounterByIdQueryHandlerTests()
    {
        _mockRepository = new Mock<IEncounterRepository>();
        _mockMapper = new Mock<IMapper>();
        _handler = new GetEncounterByIdQueryHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithExistingEncounter_ShouldReturnMappedDto()
    {
        // Arrange
        var encounterId = Guid.NewGuid();
        var query = new GetEncounterByIdQuery(encounterId);

        var encounter = Encounter.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            EncounterType.Outpatient);

        var expectedDto = new EncounterDto
        {
            Id = encounterId,
            EncounterTypeCode = "OP",
            StatusCode = "IN_PROGRESS"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<EncounterId>(id => id.Value == encounterId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(encounter);

        _mockMapper.Setup(m => m.Map<EncounterDto>(encounter))
            .Returns(expectedDto);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedDto);
    }

    [Fact]
    public async Task Handle_WithNonExistingEncounter_ShouldReturnNull()
    {
        // Arrange
        var encounterId = Guid.NewGuid();
        var query = new GetEncounterByIdQuery(encounterId);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<EncounterId>(id => id.Value == encounterId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Encounter?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _mockMapper.Verify(m => m.Map<EncounterDto>(It.IsAny<Encounter>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var query = new GetEncounterByIdQuery(Guid.NewGuid());

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Encounter?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldMapEncounterToDtoCorrectly()
    {
        // Arrange
        var encounterId = Guid.NewGuid();
        var query = new GetEncounterByIdQuery(encounterId);

        var encounter = Encounter.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            EncounterType.Emergency);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<EncounterId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(encounter);

        _mockMapper.Setup(m => m.Map<EncounterDto>(encounter))
            .Returns(new EncounterDto { Id = encounterId });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(encounterId);
    }
}
