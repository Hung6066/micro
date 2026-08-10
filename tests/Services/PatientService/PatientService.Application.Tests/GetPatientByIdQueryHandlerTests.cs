using AutoMapper;
using FluentAssertions;
using His.Hope.PatientService.Application.DTOs;
using His.Hope.PatientService.Application.UseCases.Patients.Queries;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.ValueObjects;
using Moq;

namespace His.Hope.PatientService.Application.Tests;

public class GetPatientByIdQueryHandlerTests
{
    private readonly Mock<IPatientRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly GetPatientByIdQueryHandler _handler;

    public GetPatientByIdQueryHandlerTests()
    {
        _mockRepository = new Mock<IPatientRepository>();
        _mockMapper = new Mock<IMapper>();
        _handler = new GetPatientByIdQueryHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithExistingPatient_ShouldReturnMappedDto()
    {
        var patientId = Guid.NewGuid();
        var query = new GetPatientByIdQuery(patientId);
        var patient = Patient.Register(
            new PersonName("John", "Doe"),
            new DateTime(1990, 1, 15),
            Gender.Male,
            new ContactInfo("+1234567890"),
            new Address("123 St", "District", "City", "Province", "12345", "USA"));

        var expectedDto = new PatientDto { Id = patientId, FullName = "Doe John" };

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<PatientId>(id => id.Value == patientId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        _mockMapper.Setup(m => m.Map<PatientDto>(patient)).Returns(expectedDto);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);
    }

    [Fact]
    public async Task Handle_WithNonExistingPatient_ShouldReturnNull()
    {
        var query = new GetPatientByIdQuery(Guid.NewGuid());

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeNull();
        _mockMapper.Verify(m => m.Map<PatientDto>(It.IsAny<Patient>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldQueryByIdWithCorrectId()
    {
        var patientId = Guid.NewGuid();
        var query = new GetPatientByIdQuery(patientId);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        await _handler.Handle(query, CancellationToken.None);

        _mockRepository.Verify(r => r.GetByIdAsync(
            It.Is<PatientId>(id => id.Value == patientId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
