using AutoMapper;
using FluentAssertions;
using His.Hope.PatientService.Application.DTOs;
using His.Hope.PatientService.Application.UseCases.Patients.Commands;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;
using Moq;

namespace His.Hope.PatientService.Application.Tests;

public class UpdatePatientCommandHandlerTests
{
    private readonly Mock<IPatientRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly UpdatePatientCommandHandler _handler;

    public UpdatePatientCommandHandlerTests()
    {
        _mockRepository = new Mock<IPatientRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);
        _handler = new UpdatePatientCommandHandler(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithExistingPatient_ShouldUpdateAndReturnDto()
    {
        var patientId = Guid.NewGuid();
        var patient = CreatePatient();
        var command = new UpdatePatientCommand(
            Id: patientId,
            FirstName: "Jane",
            LastName: "Smith",
            MiddleName: null,
            DateOfBirth: new DateTime(1985, 5, 20),
            GenderCode: "F",
            Phone: "+9876543210",
            Email: null,
            Street: "456 Oak Ave",
            District: "Uptown",
            City: "Gotham",
            Province: "State",
            PostalCode: "67890",
            Country: "USA");

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var expectedDto = new PatientDto { Id = patientId, FirstName = "Jane", LastName = "Smith" };
        _mockMapper.Setup(m => m.Map<PatientDto>(patient)).Returns(expectedDto);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(expectedDto);
        patient.Name.FirstName.Should().Be("Jane");
        patient.Gender.Code.Should().Be("F");
        _mockRepository.Verify(r => r.UpdateAsync(patient, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingPatient_ShouldThrowNotFoundException()
    {
        var command = new UpdatePatientCommand(
            Id: Guid.NewGuid(), FirstName: "Jane", LastName: "Smith", MiddleName: null,
            DateOfBirth: null, GenderCode: null, Phone: "+9876543210", Email: null,
            Street: "456 Oak Ave", District: "Uptown", City: "Gotham",
            Province: "State", PostalCode: null, Country: "USA");

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullGenderCode_ShouldNotChangeGender()
    {
        var patient = CreatePatient();
        var originalGender = patient.Gender;
        var command = new UpdatePatientCommand(
            Id: Guid.NewGuid(), FirstName: "Jane", LastName: "Smith", MiddleName: null,
            DateOfBirth: null, GenderCode: null, Phone: "+9876543210", Email: null,
            Street: "456 Oak Ave", District: "Uptown", City: "Gotham",
            Province: "State", PostalCode: null, Country: "USA");

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);
        _mockMapper.Setup(m => m.Map<PatientDto>(It.IsAny<Patient>())).Returns(new PatientDto());

        await _handler.Handle(command, CancellationToken.None);

        patient.Gender.Should().Be(originalGender);
    }

    private static Patient CreatePatient()
    {
        return Patient.Register(
            new PersonName("John", "Doe"),
            new DateTime(1990, 1, 15),
            Gender.Male,
            new ContactInfo("+1234567890"),
            new Address("123 St", "District", "City", "Province", "12345", "USA"));
    }
}
