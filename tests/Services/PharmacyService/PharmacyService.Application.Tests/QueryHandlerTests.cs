using AutoMapper;
using FluentAssertions;
using His.Hope.PharmacyService.Application.DTOs;
using His.Hope.PharmacyService.Application.UseCases.Medications.Queries;
using His.Hope.PharmacyService.Application.UseCases.Prescriptions.Queries;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using Moq;

namespace His.Hope.PharmacyService.Application.Tests;

public class QueryHandlerTests
{
    public class GetPrescriptionByIdQueryHandlerTests
    {
        private readonly Mock<IPrescriptionRepository> _mockRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly GetPrescriptionByIdQueryHandler _handler;

        public GetPrescriptionByIdQueryHandlerTests()
        {
            _mockRepository = new Mock<IPrescriptionRepository>();
            _mockMapper = new Mock<IMapper>();
            _handler = new GetPrescriptionByIdQueryHandler(_mockRepository.Object, _mockMapper.Object);
        }

        [Fact]
        public async Task Handle_WhenExists_ShouldReturnMappedDto()
        {
            var prescriptionId = Guid.NewGuid();
            var prescription = CreatePrescription(prescriptionId);
            var expectedDto = new PrescriptionDto { Id = prescriptionId };

            _mockRepository.Setup(r => r.GetByIdAsync(
                    It.Is<PrescriptionId>(id => id.Value == prescriptionId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(prescription);

            _mockMapper.Setup(m => m.Map<PrescriptionDto>(prescription))
                .Returns(expectedDto);

            var result = await _handler.Handle(
                new GetPrescriptionByIdQuery(prescriptionId), CancellationToken.None);

            result.Should().NotBeNull();
            result.Should().Be(expectedDto);
        }

        [Fact]
        public async Task Handle_WhenNotFound_ShouldReturnNull()
        {
            var prescriptionId = Guid.NewGuid();

            _mockRepository.Setup(r => r.GetByIdAsync(
                    It.IsAny<PrescriptionId>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Prescription?)null);

            var result = await _handler.Handle(
                new GetPrescriptionByIdQuery(prescriptionId), CancellationToken.None);

            result.Should().BeNull();
        }
    }

    public class GetPrescriptionsByPatientQueryHandlerTests
    {
        private readonly Mock<IPrescriptionRepository> _mockRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly GetPrescriptionsByPatientQueryHandler _handler;

        public GetPrescriptionsByPatientQueryHandlerTests()
        {
            _mockRepository = new Mock<IPrescriptionRepository>();
            _mockMapper = new Mock<IMapper>();
            _handler = new GetPrescriptionsByPatientQueryHandler(_mockRepository.Object, _mockMapper.Object);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedList()
        {
            var patientId = Guid.NewGuid();
            var prescriptions = new List<Prescription>
            {
                CreatePrescription(Guid.NewGuid()),
                CreatePrescription(Guid.NewGuid())
            };
            var expectedDtos = new List<PrescriptionDto>
            {
                new() { Id = Guid.NewGuid() },
                new() { Id = Guid.NewGuid() }
            };

            _mockRepository.Setup(r => r.GetByPatientIdAsync(patientId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(prescriptions);

            _mockMapper.Setup(m => m.Map<List<PrescriptionDto>>(prescriptions))
                .Returns(expectedDtos);

            var result = await _handler.Handle(
                new GetPrescriptionsByPatientQuery(patientId), CancellationToken.None);

            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(expectedDtos);
        }

        [Fact]
        public async Task Handle_WhenNoPrescriptions_ShouldReturnEmptyList()
        {
            var patientId = Guid.NewGuid();

            _mockRepository.Setup(r => r.GetByPatientIdAsync(patientId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Prescription>());

            _mockMapper.Setup(m => m.Map<List<PrescriptionDto>>(It.IsAny<List<Prescription>>()))
                .Returns(new List<PrescriptionDto>());

            var result = await _handler.Handle(
                new GetPrescriptionsByPatientQuery(patientId), CancellationToken.None);

            result.Should().BeEmpty();
        }
    }

    public class SearchPrescriptionsQueryHandlerTests
    {
        private readonly Mock<IPrescriptionRepository> _mockRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly SearchPrescriptionsQueryHandler _handler;

        public SearchPrescriptionsQueryHandlerTests()
        {
            _mockRepository = new Mock<IPrescriptionRepository>();
            _mockMapper = new Mock<IMapper>();
            _handler = new SearchPrescriptionsQueryHandler(_mockRepository.Object, _mockMapper.Object);
        }

        [Fact]
        public async Task Handle_ShouldReturnPagedResult()
        {
            var prescriptions = new List<Prescription>
            {
                CreatePrescription(Guid.NewGuid()),
                CreatePrescription(Guid.NewGuid())
            };
            var dtos = new List<PrescriptionDto>
            {
                new() { Id = Guid.NewGuid() },
                new() { Id = Guid.NewGuid() }
            };

            _mockRepository.Setup(r => r.SearchAsync(
                    "amox", 1, 20, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync((prescriptions, 2));

            _mockMapper.Setup(m => m.Map<List<PrescriptionDto>>(prescriptions))
                .Returns(dtos);

            var query = new SearchPrescriptionsQuery(SearchTerm: "amox", Page: 1, PageSize: 20);

            var result = await _handler.Handle(query, CancellationToken.None);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(2);
            result.TotalCount.Should().Be(2);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(20);
        }

        [Fact]
        public async Task Handle_WithPatientFilter_ShouldPassPatientId()
        {
            var patientId = Guid.NewGuid();

            _mockRepository.Setup(r => r.SearchAsync(
                    "", 1, 20, patientId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<Prescription>(), 0));

            _mockMapper.Setup(m => m.Map<List<PrescriptionDto>>(It.IsAny<List<Prescription>>()))
                .Returns(new List<PrescriptionDto>());

            var query = new SearchPrescriptionsQuery(PatientId: patientId);

            var result = await _handler.Handle(query, CancellationToken.None);

            result.TotalCount.Should().Be(0);
            _mockRepository.Verify(r => r.SearchAsync(
                "", 1, 20, patientId, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithStatusFilter_ShouldPassStatus()
        {
            _mockRepository.Setup(r => r.SearchAsync(
                    "", 1, 20, null, "FILLED", It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<Prescription>(), 0));

            _mockMapper.Setup(m => m.Map<List<PrescriptionDto>>(It.IsAny<List<Prescription>>()))
                .Returns(new List<PrescriptionDto>());

            var query = new SearchPrescriptionsQuery(Status: "FILLED");

            var result = await _handler.Handle(query, CancellationToken.None);

            result.TotalCount.Should().Be(0);
        }
    }

    public class GetMedicationByIdQueryHandlerTests
    {
        private readonly Mock<IMedicationRepository> _mockRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly GetMedicationByIdQueryHandler _handler;

        public GetMedicationByIdQueryHandlerTests()
        {
            _mockRepository = new Mock<IMedicationRepository>();
            _mockMapper = new Mock<IMapper>();
            _handler = new GetMedicationByIdQueryHandler(_mockRepository.Object, _mockMapper.Object);
        }

        [Fact]
        public async Task Handle_WhenExists_ShouldReturnMappedDto()
        {
            var medicationId = Guid.NewGuid();
            var medication = CreateMedication(medicationId);
            var expectedDto = new MedicationDto { Id = medicationId };

            _mockRepository.Setup(r => r.GetByIdAsync(
                    It.Is<MedicationId>(id => id.Value == medicationId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(medication);

            _mockMapper.Setup(m => m.Map<MedicationDto>(medication))
                .Returns(expectedDto);

            var result = await _handler.Handle(
                new GetMedicationByIdQuery(medicationId), CancellationToken.None);

            result.Should().NotBeNull();
            result.Should().Be(expectedDto);
        }

        [Fact]
        public async Task Handle_WhenNotFound_ShouldReturnNull()
        {
            var medicationId = Guid.NewGuid();

            _mockRepository.Setup(r => r.GetByIdAsync(
                    It.IsAny<MedicationId>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Medication?)null);

            var result = await _handler.Handle(
                new GetMedicationByIdQuery(medicationId), CancellationToken.None);

            result.Should().BeNull();
        }
    }

    public class SearchMedicationsQueryHandlerTests
    {
        private readonly Mock<IMedicationRepository> _mockRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly SearchMedicationsQueryHandler _handler;

        public SearchMedicationsQueryHandlerTests()
        {
            _mockRepository = new Mock<IMedicationRepository>();
            _mockMapper = new Mock<IMapper>();
            _handler = new SearchMedicationsQueryHandler(_mockRepository.Object, _mockMapper.Object);
        }

        [Fact]
        public async Task Handle_ShouldReturnPagedResult()
        {
            var medications = new List<Medication>
            {
                CreateMedication(Guid.NewGuid()),
                CreateMedication(Guid.NewGuid())
            };
            var dtos = new List<MedicationDto>
            {
                new() { Id = Guid.NewGuid() },
                new() { Id = Guid.NewGuid() }
            };

            _mockRepository.Setup(r => r.SearchAsync(
                    "amox", 1, 20, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync((medications, 2));

            _mockMapper.Setup(m => m.Map<List<MedicationDto>>(medications))
                .Returns(dtos);

            var query = new SearchMedicationsQuery(SearchTerm: "amox", Page: 1, PageSize: 20);

            var result = await _handler.Handle(query, CancellationToken.None);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(2);
            result.TotalCount.Should().Be(2);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(20);
        }

        [Fact]
        public async Task Handle_WithCategoryFilter_ShouldPassCategory()
        {
            _mockRepository.Setup(r => r.SearchAsync(
                    "", 1, 20, "Antibiotic", It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<Medication>(), 0));

            _mockMapper.Setup(m => m.Map<List<MedicationDto>>(It.IsAny<List<Medication>>()))
                .Returns(new List<MedicationDto>());

            var query = new SearchMedicationsQuery(Category: "Antibiotic");

            var result = await _handler.Handle(query, CancellationToken.None);

            result.TotalCount.Should().Be(0);
        }

        [Fact]
        public async Task Handle_WithDefaultParameters_ShouldUseDefaults()
        {
            _mockRepository.Setup(r => r.SearchAsync(
                    "", 1, 20, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<Medication>(), 0));

            _mockMapper.Setup(m => m.Map<List<MedicationDto>>(It.IsAny<List<Medication>>()))
                .Returns(new List<MedicationDto>());

            var query = new SearchMedicationsQuery();

            var result = await _handler.Handle(query, CancellationToken.None);

            result.TotalCount.Should().Be(0);
        }
    }

    private static Prescription CreatePrescription(Guid id)
    {
        var prescription = Prescription.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            "Amoxicillin", "500mg", "Capsule",
            "Take one capsule three times daily", null,
            30, 0, null, null);

        typeof(Entity<PrescriptionId>)
            .GetProperty(nameof(Entity<PrescriptionId>.Id))!
            .SetValue(prescription, PrescriptionId.From(id));

        return prescription;
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
