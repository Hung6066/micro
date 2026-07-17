using FluentAssertions;
using His.Hope.PharmacyService.Application.Common.Exceptions;
using His.Hope.PharmacyService.Application.UseCases.Medications.Commands;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.PharmacyService.Application.Tests;

public class DeactivateMedicationCommandHandlerTests
{
    private readonly Mock<IMedicationRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly DeactivateMedicationCommandHandler _deactivateHandler;

    public DeactivateMedicationCommandHandlerTests()
    {
        _mockRepository = new Mock<IMedicationRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _deactivateHandler = new DeactivateMedicationCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidId_ShouldDeactivateMedication()
    {
        var medicationId = Guid.NewGuid();
        var medication = CreateMedication(medicationId);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<MedicationId>(id => id.Value == medicationId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(medication);

        var command = new DeactivateMedicationCommand(medicationId);

        var result = await _deactivateHandler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        medication.IsActive.Should().BeFalse();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ShouldThrow()
    {
        var medicationId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<MedicationId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medication?)null);

        var command = new DeactivateMedicationCommand(medicationId);

        var act = async () => await _deactivateHandler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{medicationId}*");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Medication CreateMedication(Guid id)
    {
        var medication = Medication.Create("Amoxicillin", "Capsule", "500mg");

        typeof(Entity<MedicationId>)
            .GetProperty(nameof(Entity<MedicationId>.Id))!
            .SetValue(medication, MedicationId.From(id));

        return medication;
    }
}

public class ReactivateMedicationCommandHandlerTests
{
    private readonly Mock<IMedicationRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly ReactivateMedicationCommandHandler _reactivateHandler;

    public ReactivateMedicationCommandHandlerTests()
    {
        _mockRepository = new Mock<IMedicationRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _reactivateHandler = new ReactivateMedicationCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidId_ShouldReactivateMedication()
    {
        var medicationId = Guid.NewGuid();
        var medication = CreateMedication(medicationId);
        medication.Deactivate();

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<MedicationId>(id => id.Value == medicationId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(medication);

        var command = new ReactivateMedicationCommand(medicationId);

        var result = await _reactivateHandler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        medication.IsActive.Should().BeTrue();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ShouldThrow()
    {
        var medicationId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<MedicationId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medication?)null);

        var command = new ReactivateMedicationCommand(medicationId);

        var act = async () => await _reactivateHandler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{medicationId}*");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Medication CreateMedication(Guid id)
    {
        var medication = Medication.Create("Amoxicillin", "Capsule", "500mg");

        typeof(Entity<MedicationId>)
            .GetProperty(nameof(Entity<MedicationId>.Id))!
            .SetValue(medication, MedicationId.From(id));

        return medication;
    }
}
