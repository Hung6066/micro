using AutoMapper;
using FluentAssertions;
using His.Hope.PatientService.Application.DTOs;
using His.Hope.PatientService.Application.UseCases.Patients.Commands;
using His.Hope.PatientService.Application.UseCases.Patients.Queries;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;
using Moq;

namespace His.Hope.PatientService.Application.Tests;

public class AdditionalHandlerEdgeCaseTests
{
    [Fact]
    public async Task CreateHandler_WithNullEmail_ShouldCreatePatientSuccessfully()
    {
        var mockRepo = new Mock<IPatientRepository>();
        var mockMapper = new Mock<IMapper>();
        var mockDispatcher = new Mock<DomainEventDispatcher>(Mock.Of<MediatR.IMediator>());
        var mockUow = new Mock<IUnitOfWork>();
        mockRepo.Setup(r => r.UnitOfWork).Returns(mockUow.Object);

        var handler = new CreatePatientCommandHandler(mockRepo.Object, mockMapper.Object, mockDispatcher.Object);

        var command = new CreatePatientCommand(
            "John", "Doe", null, new DateTime(1990, 1, 15), "M", "+1234567890", null,
            "123 St", "District", "City", "State", "12345", "USA", null, null);

        mockMapper.Setup(m => m.Map<PatientDto>(It.IsAny<Patient>())).Returns(new PatientDto());

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        mockRepo.Verify(r => r.AddAsync(
            It.Is<Patient>(p => p.ContactInfo.Email == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateHandler_WithAllOptionals_ShouldCreatePatient()
    {
        var mockRepo = new Mock<IPatientRepository>();
        var mockMapper = new Mock<IMapper>();
        var mockDispatcher = new Mock<DomainEventDispatcher>(Mock.Of<MediatR.IMediator>());
        var mockUow = new Mock<IUnitOfWork>();
        mockRepo.Setup(r => r.UnitOfWork).Returns(mockUow.Object);

        var handler = new CreatePatientCommandHandler(mockRepo.Object, mockMapper.Object, mockDispatcher.Object);

        var command = new CreatePatientCommand(
            "Alice", "Johnson", "Marie", new DateTime(1985, 5, 20), "F", "+9876543210", "alice@example.com",
            "456 Oak Ave", "Uptown", "Gotham", "State", "67890", "USA", "INS-999", "NID-123");

        mockMapper.Setup(m => m.Map<PatientDto>(It.IsAny<Patient>())).Returns(new PatientDto());

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        mockRepo.Verify(r => r.AddAsync(
            It.Is<Patient>(p =>
                p.InsuranceId == "INS-999" &&
                p.NationalId == "NID-123" &&
                p.Name.MiddleName == "Marie"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdHandler_WithCorrectId_ShouldPassCorrectId()
    {
        var patientId = Guid.NewGuid();
        var mockRepo = new Mock<IPatientRepository>();
        var mockMapper = new Mock<IMapper>();
        var handler = new GetPatientByIdQueryHandler(mockRepo.Object, mockMapper.Object);

        mockRepo.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        await handler.Handle(new GetPatientByIdQuery(patientId), CancellationToken.None);

        mockRepo.Verify(r => r.GetByIdAsync(
            It.Is<PatientId>(id => id.Value == patientId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchHandler_WithNullSearchTerm_ShouldPassNull()
    {
        var mockRepo = new Mock<IPatientRepository>();
        var mockMapper = new Mock<IMapper>();
        var handler = new SearchPatientsQueryHandler(mockRepo.Object, mockMapper.Object);

        mockRepo.Setup(r => r.SearchAsync(null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Patient>(), 0));
        mockMapper.Setup(m => m.Map<List<PatientDto>>(It.IsAny<List<Patient>>())).Returns(new List<PatientDto>());

        await handler.Handle(new SearchPatientsQuery(null, 1, 20), CancellationToken.None);

        mockRepo.Verify(r => r.SearchAsync(null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateHandler_ShouldDeactivateAndSave()
    {
        var patient = Patient.Register(
            new PersonName("John", "Doe"),
            new DateTime(1990, 1, 15),
            Gender.Male,
            new ContactInfo("+1234567890"),
            new Address("123 St", "D", "C", "P", "1", "USA"));

        var mockRepo = new Mock<IPatientRepository>();
        var mockUow = new Mock<IUnitOfWork>();
        mockRepo.Setup(r => r.UnitOfWork).Returns(mockUow.Object);
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<PatientId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var handler = new DeactivatePatientCommandHandler(mockRepo.Object);

        await handler.Handle(new DeactivatePatientCommand(Guid.NewGuid()), CancellationToken.None);

        patient.IsActive.Should().BeFalse();
        mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReactivateHandler_ShouldReactivateAndSave()
    {
        var patient = Patient.Register(
            new PersonName("Jane", "Smith"),
            new DateTime(1985, 5, 20),
            Gender.Female,
            new ContactInfo("+9876543210"),
            new Address("456 Oak Ave", "U", "Gotham", "S", "67890", "USA"));
        patient.Deactivate();

        var mockRepo = new Mock<IPatientRepository>();
        var mockUow = new Mock<IUnitOfWork>();
        mockRepo.Setup(r => r.UnitOfWork).Returns(mockUow.Object);
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<PatientId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var handler = new ReactivatePatientCommandHandler(mockRepo.Object);

        await handler.Handle(new ReactivatePatientCommand(Guid.NewGuid()), CancellationToken.None);

        patient.IsActive.Should().BeTrue();
        mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
