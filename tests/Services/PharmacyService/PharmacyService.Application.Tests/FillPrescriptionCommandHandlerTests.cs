using FluentAssertions;
using His.Hope.PharmacyService.Application.Common.Exceptions;
using His.Hope.PharmacyService.Application.UseCases.Prescriptions.Commands;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.PharmacyService.Application.Tests;

public class FillPrescriptionCommandHandlerTests
{
    private readonly Mock<IPrescriptionRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly FillPrescriptionCommandHandler _handler;

    public FillPrescriptionCommandHandlerTests()
    {
        _mockRepository = new Mock<IPrescriptionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new FillPrescriptionCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidId_ShouldFillPrescription()
    {
        var prescriptionId = Guid.NewGuid();
        var prescription = CreatePrescription(prescriptionId);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<PrescriptionId>(id => id.Value == prescriptionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prescription);

        var command = new FillPrescriptionCommand(prescriptionId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        prescription.Status.Should().Be(PrescriptionStatus.Filled);
        prescription.FilledDate.Should().NotBeNull();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ShouldThrow()
    {
        var prescriptionId = Guid.NewGuid();

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<PrescriptionId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prescription?)null);

        var command = new FillPrescriptionCommand(prescriptionId);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{prescriptionId}*");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Prescription CreatePrescription(Guid id)
    {
        var prescription = Prescription.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Amoxicillin",
            "500mg",
            "Capsule",
            "Take one capsule three times daily",
            null,
            30,
            0,
            null,
            null);

        typeof(Entity<PrescriptionId>)
            .GetProperty(nameof(Entity<PrescriptionId>.Id))!
            .SetValue(prescription, PrescriptionId.From(id));

        return prescription;
    }
}
