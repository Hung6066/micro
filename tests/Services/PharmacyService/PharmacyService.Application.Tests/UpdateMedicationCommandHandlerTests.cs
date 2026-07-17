using AutoMapper;
using FluentAssertions;
using His.Hope.PharmacyService.Application.Common.Exceptions;
using His.Hope.PharmacyService.Application.DTOs;
using His.Hope.PharmacyService.Application.UseCases.Medications.Commands;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.PharmacyService.Application.Tests;

public class UpdateMedicationCommandHandlerTests
{
    private readonly Mock<IMedicationRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly UpdateMedicationCommandHandler _handler;

    public UpdateMedicationCommandHandlerTests()
    {
        _mockRepository = new Mock<IMedicationRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new UpdateMedicationCommandHandler(
            _mockRepository.Object,
            _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldUpdateAndReturnDto()
    {
        var medicationId = Guid.NewGuid();
        var medication = CreateMedication(medicationId);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<MedicationId>(id => id.Value == medicationId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(medication);

        var command = new UpdateMedicationCommand(
            Id: medicationId,
            Name: "Updated Name",
            GenericName: "Generic",
            BrandName: "Brand",
            DosageForm: "Tablet",
            Strength: "250mg",
            Route: "Oral",
            Category: "Antibiotic",
            Manufacturer: "PharmaCorp",
            RequiresPrescription: true);

        var expectedDto = new MedicationDto
        {
            Id = medicationId,
            Name = "Updated Name"
        };

        _mockMapper.Setup(m => m.Map<MedicationDto>(It.IsAny<Medication>()))
            .Returns(expectedDto);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);

        medication.Name.Should().Be("Updated Name");
        medication.GenericName.Should().Be("Generic");
        medication.DosageForm.Should().Be("Tablet");
        medication.Strength.Should().Be("250mg");

        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<Medication>(m => m.Name == "Updated Name"),
            It.IsAny<CancellationToken>()), Times.Once);

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

        var command = new UpdateMedicationCommand(
            Id: medicationId,
            Name: "Name",
            GenericName: null,
            BrandName: null,
            DosageForm: "Tablet",
            Strength: "500mg",
            Route: null,
            Category: null,
            Manufacturer: null,
            RequiresPrescription: false);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{medicationId}*");
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Medication>(), It.IsAny<CancellationToken>()), Times.Never);
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
