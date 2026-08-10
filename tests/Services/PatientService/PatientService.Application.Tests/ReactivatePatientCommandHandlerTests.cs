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

public class ReactivatePatientCommandHandlerTests
{
    private readonly Mock<IPatientRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly ReactivatePatientCommandHandler _handler;

    public ReactivatePatientCommandHandlerTests()
    {
        _mockRepository = new Mock<IPatientRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);
        _handler = new ReactivatePatientCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithExistingDeactivatedPatient_ShouldReactivateAndSave()
    {
        var command = new ReactivatePatientCommand(Guid.NewGuid());
        var patient = CreateDeactivatedPatient();

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        patient.IsActive.Should().BeTrue();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingPatient_ShouldThrowNotFoundException()
    {
        var command = new ReactivatePatientCommand(Guid.NewGuid());

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRaiseReactivatedDomainEvent()
    {
        var patient = CreateDeactivatedPatient();

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        patient.ClearDomainEvents();
        await _handler.Handle(new ReactivatePatientCommand(Guid.NewGuid()), CancellationToken.None);

        patient.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<His.Hope.PatientService.Domain.Events.PatientReactivatedDomainEvent>();
    }

    [Fact]
    public async Task Handle_AlreadyActive_ShouldRemainActive()
    {
        var patient = CreateDeactivatedPatient();
        patient.Reactivate();

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PatientId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        await _handler.Handle(new ReactivatePatientCommand(Guid.NewGuid()), CancellationToken.None);

        patient.IsActive.Should().BeTrue();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Patient CreateDeactivatedPatient()
    {
        var patient = Patient.Register(
            new PersonName("Jane", "Smith"),
            new DateTime(1985, 5, 20),
            Gender.Female,
            new ContactInfo("+9876543210"),
            new Address("456 Oak Ave", "Uptown", "Gotham", "State", "67890", "USA"));
        patient.Deactivate();
        return patient;
    }
}
