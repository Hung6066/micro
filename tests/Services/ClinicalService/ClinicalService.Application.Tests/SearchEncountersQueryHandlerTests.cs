using AutoMapper;
using FluentAssertions;
using His.Hope.ClinicalService.Application.DTOs;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Queries;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;
using Moq;

namespace His.Hope.ClinicalService.Application.Tests;

public class SearchEncountersQueryHandlerTests
{
    private readonly Mock<IEncounterRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly SearchEncountersQueryHandler _handler;

    public SearchEncountersQueryHandlerTests()
    {
        _mockRepository = new Mock<IEncounterRepository>();
        _mockMapper = new Mock<IMapper>();
        _handler = new SearchEncountersQueryHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithSearchTerm_ShouldReturnPagedResults()
    {
        var query = new SearchEncountersQuery("chest", 1, 20);
        var encounters = new List<Encounter>
        {
            Encounter.Start(Guid.NewGuid(), Guid.NewGuid(), EncounterType.Emergency),
            Encounter.Start(Guid.NewGuid(), Guid.NewGuid(), EncounterType.Outpatient),
        };
        var dtos = new List<EncounterDto> { new(), new() };

        _mockRepository.Setup(r => r.SearchAsync("chest", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((encounters, 2));

        _mockMapper.Setup(m => m.Map<List<EncounterDto>>(encounters)).Returns(dtos);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeSameAs(dtos);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_WithNoResults_ShouldReturnEmptyPagedResult()
    {
        var query = new SearchEncountersQuery("NonExistent", 1, 20);

        _mockRepository.Setup(r => r.SearchAsync("NonExistent", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Encounter>(), 0));

        _mockMapper.Setup(m => m.Map<List<EncounterDto>>(It.IsAny<List<Encounter>>())).Returns(new List<EncounterDto>());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithNullSearchTerm_ShouldPassEmptyString()
    {
        var query = new SearchEncountersQuery(null, 1, 20);

        _mockRepository.Setup(r => r.SearchAsync(string.Empty, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Encounter>(), 0));

        _mockMapper.Setup(m => m.Map<List<EncounterDto>>(It.IsAny<List<Encounter>>())).Returns(new List<EncounterDto>());

        await _handler.Handle(query, CancellationToken.None);

        _mockRepository.Verify(r => r.SearchAsync(string.Empty, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}
