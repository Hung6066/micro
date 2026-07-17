using AutoMapper;
using FluentAssertions;
using His.Hope.BillingService.Application.DTOs;
using His.Hope.BillingService.Application.UseCases.Invoices.Commands;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using Moq;
using Xunit;

namespace His.Hope.BillingService.Application.Tests;

public class CreateInvoiceCommandHandlerTests
{
    private readonly Mock<IInvoiceRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreateInvoiceCommandHandler _handler;

    public CreateInvoiceCommandHandlerTests()
    {
        _mockRepository = new Mock<IInvoiceRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new CreateInvoiceCommandHandler(
            _mockRepository.Object,
            _mockMapper.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesInvoiceAndReturnsDto()
    {
        var command = new CreateInvoiceCommand(
            PatientId: Guid.NewGuid(),
            EncounterId: Guid.NewGuid(),
            InvoiceDate: DateTime.UtcNow,
            DueDate: DateTime.UtcNow.AddDays(30),
            InvoiceNumber: "INV-2024-001",
            Notes: "Regular consultation",
            LineItems: new List<LineItemInput>
            {
                new("Consultation fee", 1, 150.00m, "CONS001", "SERVICE")
            });

        var expectedDto = new InvoiceDto
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-2024-001",
            TotalAmount = 150.00m
        };

        _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>()))
            .Returns(expectedDto);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Invoice>(inv =>
                inv.InvoiceNumber == "INV-2024-001" &&
                inv.PatientId == command.PatientId &&
                inv.Status.Code == "DRAFT"),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleLineItems_CreatesInvoiceWithItems()
    {
        var command = new CreateInvoiceCommand(
            PatientId: Guid.NewGuid(),
            EncounterId: null,
            InvoiceDate: DateTime.UtcNow,
            DueDate: null,
            InvoiceNumber: "INV-2024-002",
            Notes: null,
            LineItems: new List<LineItemInput>
            {
                new("Consultation fee", 1, 150.00m, "CONS001", "SERVICE"),
                new("Lab work", 2, 75.00m, "LAB001", "LAB"),
                new("Medication", 1, 25.50m, "MED001", "MEDICATION")
            });

        _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>()))
            .Returns(new InvoiceDto());

        await _handler.Handle(command, CancellationToken.None);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Invoice>(inv => inv.LineItems.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithLineItemWithoutType_CreatesInvoice()
    {
        var command = new CreateInvoiceCommand(
            PatientId: Guid.NewGuid(),
            EncounterId: null,
            InvoiceDate: DateTime.UtcNow,
            DueDate: null,
            InvoiceNumber: "INV-2024-003",
            Notes: null,
            LineItems: new List<LineItemInput>
            {
                new("Generic item", 1, 100.00m, null, null)
            });

        _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>()))
            .Returns(new InvoiceDto());

        await _handler.Handle(command, CancellationToken.None);

        _mockRepository.Verify(r => r.AddAsync(
            It.Is<Invoice>(inv =>
                inv.LineItems.Count == 1 &&
                inv.LineItems.First().ItemCode == null &&
                inv.LineItems.First().ItemType == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MapsInvoiceToDtoCorrectly()
    {
        var command = new CreateInvoiceCommand(
            PatientId: Guid.NewGuid(),
            EncounterId: null,
            InvoiceDate: DateTime.UtcNow,
            DueDate: null,
            InvoiceNumber: "INV-2024-004",
            Notes: "Test",
            LineItems: new List<LineItemInput>
            {
                new("Item", 1, 50.00m, null, "SERVICE")
            });

        _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>()))
            .Returns(new InvoiceDto());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        _mockMapper.Verify(m => m.Map<InvoiceDto>(It.Is<Invoice>(inv =>
            inv.InvoiceNumber == "INV-2024-004" &&
            inv.Notes == "Test")), Times.Once);
    }
}
