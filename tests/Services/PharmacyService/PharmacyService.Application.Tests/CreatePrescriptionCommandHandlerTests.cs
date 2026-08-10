using AutoMapper;
using FluentAssertions;
using His.Hope.PharmacyService.Application.DTOs;
using His.Hope.PharmacyService.Application.UseCases.Prescriptions.Commands;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.PharmacyService.Application.Tests;

public class CreatePrescriptionCommandHandlerTests
{
    private readonly Mock<IPrescriptionRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<DomainEventDispatcher> _mockEventDispatcher;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreatePrescriptionCommandHandler _handler;

    public CreatePrescriptionCommandHandlerTests()
    {
        _mockRepository = new Mock<IPrescriptionRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockEventDispatcher = new Mock<DomainEventDispatcher>(Mock.Of<MediatR.IMediator>());
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new CreatePrescriptionCommandHandler(
            _mockRepository.Object,
            _mockMapper.Object,
            _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreatePrescriptionAndReturnDto()
    {
        var command = new CreatePrescriptionCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            MedicationId: Guid.NewGuid(),
            MedicationName: "Amoxicillin",
            Strength: "500mg",
            DosageForm: "Capsule",
            DosageInstructions: "Take one capsule three times daily",
            Route: "Oral",
            Quantity: 30,
            Refills: 2,
            Notes: "Take with food",
            ExpiryDate: DateTime.UtcNow.AddMonths(6));

        var expectedDto = new PrescriptionDto
        {
            Id = Guid.NewGuid(),
            MedicationName = "Amoxicillin",
            Strength = "500mg"
        };

        _mockMapper.Setup(m => m.Map<PrescriptionDto>(It.IsAny<Prescription>()))
            .Returns(expectedDto);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Prescription>(p =>
                p.MedicationName == "Amoxicillin" &&
                p.Strength == "500mg" &&
                p.Quantity == 30 &&
                p.Status == PrescriptionStatus.Prescribed),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldMapToDtoCorrectly()
    {
        var command = new CreatePrescriptionCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            MedicationId: null,
            MedicationName: "Ibuprofen",
            Strength: "200mg",
            DosageForm: "Tablet",
            DosageInstructions: "Take one tablet twice daily",
            Route: null,
            Quantity: 60,
            Refills: 0,
            Notes: null,
            ExpiryDate: null);

        _mockMapper.Setup(m => m.Map<PrescriptionDto>(It.IsAny<Prescription>()))
            .Returns(new PrescriptionDto());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        _mockMapper.Verify(m => m.Map<PrescriptionDto>(It.Is<Prescription>(p =>
            p.MedicationName == "Ibuprofen" &&
            p.Strength == "200mg" &&
            p.Quantity == 60)), Times.Once);
    }
}
