using FluentAssertions;
using His.Hope.PatientService.Application.UseCases.Patients.Commands;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

using His.Hope.SharedKernel.Domain.ValueObjects;
using Moq;

namespace His.Hope.PatientService.Application.Tests;

public class DeactivatePatientCommandHandlerTests
{
    private readonly Mock<IPatientRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly DeactivatePatientCommandHandler _handler;

    public DeactivatePatientCommandHandlerTests()
    {
        _mockRepository = new Mock<IPatientRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);
        _handler = new DeactivatePatientCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithExistingPatient_ShouldDeactivateAndSave()
    {
        var command = new DeactivatePatientCommand(Guid.NewGuid());
        var patient = CreatePatient();

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        patient.IsActive.Should().BeFalse();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingPatient_ShouldThrowNotFoundException()
    {
        var patientId = Guid.NewGuid();
        var command = new DeactivatePatientCommand(patientId);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*'Patient'*'{patientId}'*");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRaiseDeactivatedDomainEvent()
    {
        var patient = CreatePatient();
        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        patient.ClearDomainEvents();
        await _handler.Handle(new DeactivatePatientCommand(Guid.NewGuid()), CancellationToken.None);

        patient.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<His.Hope.PatientService.Domain.Events.PatientDeactivatedDomainEvent>();
    }

    [Fact]
    public async Task Handle_AlreadyDeactivated_ShouldRemainDeactivated()
    {
        var patient = CreatePatient();
        patient.Deactivate();

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        await _handler.Handle(new DeactivatePatientCommand(Guid.NewGuid()), CancellationToken.None);

        patient.IsActive.Should().BeFalse();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
