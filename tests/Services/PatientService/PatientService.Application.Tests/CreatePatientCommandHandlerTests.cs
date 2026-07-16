using AutoMapper;
using FluentAssertions;
using His.Hope.PatientService.Application.DTOs;
using His.Hope.PatientService.Application.UseCases.Patients.Commands;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;
using Moq;

namespace His.Hope.PatientService.Application.Tests;

public class CreatePatientCommandHandlerTests
{
    private readonly Mock<IPatientRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<DomainEventDispatcher> _mockEventDispatcher;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreatePatientCommandHandler _handler;

    public CreatePatientCommandHandlerTests()
    {
        _mockRepository = new Mock<IPatientRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockEventDispatcher = new Mock<DomainEventDispatcher>(Mock.Of<MediatR.IMediator>());
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new CreatePatientCommandHandler(
            _mockRepository.Object,
            _mockMapper.Object,
            _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreatePatientAndReturnDto()
    {
        // Arrange
        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            MiddleName: "M",
            DateOfBirth: new DateTime(1990, 1, 15),
            GenderCode: "M",
            Phone: "+1234567890",
            Email: "john@example.com",
            Street: "123 Main St",
            District: "Downtown",
            City: "Metropolis",
            Province: "State",
            PostalCode: "12345",
            Country: "USA",
            InsuranceId: "INS-123",
            NationalId: "NID-456");

        var expectedDto = new PatientDto
        {
            Id = Guid.NewGuid(),
            FullName = "Doe John",
            FirstName = "John",
            LastName = "Doe"
        };

        _mockMapper.Setup(m => m.Map<PatientDto>(It.IsAny<Patient>()))
            .Returns(expectedDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedDto);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Patient>(p =>
                p.Name.FirstName == "John" &&
                p.Name.LastName == "Doe" &&
                p.Gender == Gender.Male &&
                p.ContactInfo.Phone == "+1234567890" &&
                p.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInsuranceId_ShouldSetInsurance()
    {
        // Arrange
        var command = new CreatePatientCommand(
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
            PostalCode: null,
            Country: "USA",
            InsuranceId: "INS-999",
            NationalId: null);

        _mockMapper.Setup(m => m.Map<PatientDto>(It.IsAny<Patient>()))
            .Returns(new PatientDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Patient>(p => p.InsuranceId == "INS-999"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutInsuranceId_ShouldNotSetInsurance()
    {
        // Arrange
        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            DateOfBirth: new DateTime(1990, 1, 15),
            GenderCode: "M",
            Phone: "+1234567890",
            Email: null,
            Street: "123 Main St",
            District: "Downtown",
            City: "Metropolis",
            Province: "State",
            PostalCode: null,
            Country: "USA",
            InsuranceId: null,
            NationalId: null);

        _mockMapper.Setup(m => m.Map<PatientDto>(It.IsAny<Patient>()))
            .Returns(new PatientDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Patient>(p => p.InsuranceId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidGenderCode_ShouldThrow()
    {
        // Arrange
        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            DateOfBirth: new DateTime(1990, 1, 15),
            GenderCode: "INVALID",
            Phone: "+1234567890",
            Email: null,
            Street: "123 Main St",
            District: "Downtown",
            City: "Metropolis",
            Province: "State",
            PostalCode: null,
            Country: "USA",
            InsuranceId: null,
            NationalId: null);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyDistrict_ShouldUseDash()
    {
        // Arrange
        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            DateOfBirth: new DateTime(1990, 1, 15),
            GenderCode: "M",
            Phone: "+1234567890",
            Email: null,
            Street: "123 Main St",
            District: "",
            City: "Metropolis",
            Province: "State",
            PostalCode: null,
            Country: "USA",
            InsuranceId: null,
            NationalId: null);

        _mockMapper.Setup(m => m.Map<PatientDto>(It.IsAny<Patient>()))
            .Returns(new PatientDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Patient>(p => p.Address.District == "-"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNationalId_ShouldSetNationalId()
    {
        // Arrange
        var command = new CreatePatientCommand(
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            DateOfBirth: new DateTime(1990, 1, 15),
            GenderCode: "M",
            Phone: "+1234567890",
            Email: null,
            Street: "123 Main St",
            District: "Downtown",
            City: "Metropolis",
            Province: "State",
            PostalCode: null,
            Country: "USA",
            InsuranceId: null,
            NationalId: "NAT-456");

        _mockMapper.Setup(m => m.Map<PatientDto>(It.IsAny<Patient>()))
            .Returns(new PatientDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Patient>(p => p.NationalId == "NAT-456"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldMapToDtoCorrectly()
    {
        // Arrange
        var command = new CreatePatientCommand(
            FirstName: "Alice",
            LastName: "Johnson",
            MiddleName: null,
            DateOfBirth: new DateTime(1995, 3, 10),
            GenderCode: "F",
            Phone: "+1112223333",
            Email: null,
            Street: "789 Pine St",
            District: "Suburb",
            City: "Smallville",
            Province: "State",
            PostalCode: "67890",
            Country: "USA",
            InsuranceId: null,
            NationalId: null);

        _mockMapper.Setup(m => m.Map<PatientDto>(It.IsAny<Patient>()))
            .Returns(new PatientDto());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _mockMapper.Verify(m => m.Map<PatientDto>(It.Is<Patient>(p =>
            p.Name.FirstName == "Alice" &&
            p.Name.LastName == "Johnson" &&
            p.Gender == Gender.Female)), Times.Once);
    }
}
