using AutoMapper;
using FluentAssertions;
using His.Hope.BillingService.Application.Common.Exceptions;
using His.Hope.BillingService.Application.DTOs;
using His.Hope.BillingService.Application.UseCases.Invoices.Commands;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Moq;
using Xunit;

namespace His.Hope.BillingService.Application.Tests;

public class RecordPaymentCommandHandlerTests
{
    private readonly Mock<IInvoiceRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly RecordPaymentCommandHandler _handler;

    public RecordPaymentCommandHandlerTests()
    {
        _mockRepository = new Mock<IInvoiceRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new RecordPaymentCommandHandler(
            _mockRepository.Object,
            _mockMapper.Object);
    }

    private Invoice CreateSubmittedInvoice()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(), Guid.NewGuid(), "INV-001",
            DateTime.UtcNow, null, null);
        var lineItem = InvoiceLineItem.Create(
            invoice.Id, "Service", 1, 200.00m, null, InvoiceLineItemType.Service);
        invoice.AddLineItem(lineItem);
        return invoice;
    }

    [Fact]
    public async Task Handle_WithValidCommand_RecordsPaymentAndReturnsDto()
    {
        var invoice = CreateSubmittedInvoice();
        var invoiceId = invoice.Id.Value;
        var patientId = Guid.NewGuid();

        var command = new RecordPaymentCommand(
            InvoiceId: invoiceId,
            PatientId: patientId,
            Amount: 100.00m,
            PaymentDate: DateTime.UtcNow,
            MethodCode: "CREDIT_CARD",
            ReferenceNumber: "REF-001",
            Notes: "Partial payment");

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<InvoiceId>(id => id.Value == invoiceId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var expectedDto = new InvoiceDto { Id = invoiceId };
        _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>()))
            .Returns(expectedDto);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);
        invoice.Payments.Should().HaveCount(1);
        invoice.PaidAmount.Should().Be(100.00m);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenInvoiceNotFound_ThrowsNotFoundException()
    {
        var command = new RecordPaymentCommand(
            InvoiceId: Guid.NewGuid(),
            PatientId: Guid.NewGuid(),
            Amount: 100.00m,
            PaymentDate: DateTime.UtcNow,
            MethodCode: "CASH",
            ReferenceNumber: null,
            Notes: null);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<InvoiceId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice?)null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Invoice*");

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FullPayment_MarksInvoiceAsPaid()
    {
        var invoice = CreateSubmittedInvoice();
        var invoiceId = invoice.Id.Value;

        var command = new RecordPaymentCommand(
            InvoiceId: invoiceId,
            PatientId: Guid.NewGuid(),
            Amount: 200.00m,
            PaymentDate: DateTime.UtcNow,
            MethodCode: "BANK_TRANSFER",
            ReferenceNumber: "BT-001",
            Notes: null);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<InvoiceId>(id => id.Value == invoiceId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>()))
            .Returns(new InvoiceDto());

        await _handler.Handle(command, CancellationToken.None);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.BalanceDue.Should().Be(0);
    }
}
