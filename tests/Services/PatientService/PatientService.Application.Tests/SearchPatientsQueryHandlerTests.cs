using AutoMapper;
using FluentAssertions;
using His.Hope.PatientService.Application.DTOs;
using His.Hope.PatientService.Application.UseCases.Patients.Queries;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;
using His.Hope.PatientService.Domain.ValueObjects;
using Moq;

namespace His.Hope.PatientService.Application.Tests;

public class SearchPatientsQueryHandlerTests
{
    private readonly Mock<IPatientRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly SearchPatientsQueryHandler _handler;

    public SearchPatientsQueryHandlerTests()
    {
        _mockRepository = new Mock<IPatientRepository>();
        _mockMapper = new Mock<IMapper>();
        _handler = new SearchPatientsQueryHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithSearchTerm_ShouldReturnPagedResults()
    {
        var query = new SearchPatientsQuery("John", 1, 20);
        var patients = new List<Patient>
        {
            Patient.Register(new PersonName("John", "Doe"), new DateTime(1990, 1, 1), Gender.Male, new ContactInfo("+123"), new Address("St", "D", "C", "P", "1", "U")),
            Patient.Register(new PersonName("Johnny", "Smith"), new DateTime(1992, 2, 2), Gender.Male, new ContactInfo("+456"), new Address("Ave", "D", "C", "P", "2", "U")),
        };
        var dtos = new List<PatientDto> { new(), new() };

        _mockRepository.Setup(r => r.SearchAsync("John", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((patients, 2));

        _mockMapper.Setup(m => m.Map<List<PatientDto>>(patients)).Returns(dtos);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().BeSameAs(dtos);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_WithNoResults_ShouldReturnEmptyPagedResult()
    {
        var query = new SearchPatientsQuery("NonExistent", 1, 20);

        _mockRepository.Setup(r => r.SearchAsync("NonExistent", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Patient>(), 0));

        _mockMapper.Setup(m => m.Map<List<PatientDto>>(It.IsAny<List<Patient>>())).Returns(new List<PatientDto>());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithPaging_ShouldPassCorrectPageAndSize()
    {
        var query = new SearchPatientsQuery("test", 2, 10);

        _mockRepository.Setup(r => r.SearchAsync("test", 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Patient>(), 0));

        _mockMapper.Setup(m => m.Map<List<PatientDto>>(It.IsAny<List<Patient>>())).Returns(new List<PatientDto>());

        await _handler.Handle(query, CancellationToken.None);

        _mockRepository.Verify(r => r.SearchAsync("test", 2, 10, It.IsAny<CancellationToken>()), Times.Once);
    }
}
