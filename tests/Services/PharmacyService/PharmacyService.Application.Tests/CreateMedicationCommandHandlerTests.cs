using AutoMapper;
using FluentAssertions;
using His.Hope.PharmacyService.Application.DTOs;
using His.Hope.PharmacyService.Application.UseCases.Medications.Commands;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using Moq;

namespace His.Hope.PharmacyService.Application.Tests;

public class CreateMedicationCommandHandlerTests
{
    private readonly Mock<IMedicationRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<DomainEventDispatcher> _mockEventDispatcher;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreateMedicationCommandHandler _handler;

    public CreateMedicationCommandHandlerTests()
    {
        _mockRepository = new Mock<IMedicationRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockEventDispatcher = new Mock<DomainEventDispatcher>(Mock.Of<MediatR.IMediator>());
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new CreateMedicationCommandHandler(
            _mockRepository.Object,
            _mockMapper.Object,
            _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateMedicationAndReturnDto()
    {
        var command = new CreateMedicationCommand(
            Name: "Amoxicillin",
            GenericName: "Amoxicillin",
            BrandName: "Amoxil",
            DosageForm: "Capsule",
            Strength: "500mg",
            Route: "Oral",
            Category: "Antibiotic",
            Manufacturer: "TestPharma",
            RequiresPrescription: true);

        var expectedDto = new MedicationDto
        {
            Id = Guid.NewGuid(),
            Name = "Amoxicillin",
            Strength = "500mg"
        };

        _mockMapper.Setup(m => m.Map<MedicationDto>(It.IsAny<Medication>()))
            .Returns(expectedDto);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Medication>(m =>
                m.Name == "Amoxicillin" &&
                m.DosageForm == "Capsule" &&
                m.Strength == "500mg" &&
                m.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldMapToDtoCorrectly()
    {
        var command = new CreateMedicationCommand(
            Name: "Ibuprofen",
            GenericName: null,
            BrandName: null,
            DosageForm: "Tablet",
            Strength: "200mg",
            Route: null,
            Category: null,
            Manufacturer: null,
            RequiresPrescription: false);

        _mockMapper.Setup(m => m.Map<MedicationDto>(It.IsAny<Medication>()))
            .Returns(new MedicationDto());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        _mockMapper.Verify(m => m.Map<MedicationDto>(It.Is<Medication>(p =>
            p.Name == "Ibuprofen" &&
            p.Strength == "200mg")), Times.Once);
    }
}
