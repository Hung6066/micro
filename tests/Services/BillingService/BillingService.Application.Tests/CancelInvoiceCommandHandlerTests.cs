using FluentAssertions;
using His.Hope.BillingService.Application.Common.Exceptions;
using His.Hope.BillingService.Application.UseCases.Invoices.Commands;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Moq;
using Xunit;

namespace His.Hope.BillingService.Application.Tests;

public class CancelInvoiceCommandHandlerTests
{
    private readonly Mock<IInvoiceRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CancelInvoiceCommandHandler _handler;

    public CancelInvoiceCommandHandlerTests()
    {
        _mockRepository = new Mock<IInvoiceRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new CancelInvoiceCommandHandler(_mockRepository.Object);
    }

    private Invoice CreateDraftInvoice()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(), Guid.NewGuid(), "INV-001",
            DateTime.UtcNow, null, null);
        var lineItem = InvoiceLineItem.Create(
            invoice.Id, "Service", 1, 100m, null, InvoiceLineItemType.Service);
        invoice.AddLineItem(lineItem);
        return invoice;
    }

    [Fact]
    public async Task Handle_WithValidCommand_CancelsInvoice()
    {
        var invoice = CreateDraftInvoice();
        var invoiceId = invoice.Id.Value;
        const string reason = "Patient cancelled appointment";

        var command = new CancelInvoiceCommand(invoiceId, reason);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<InvoiceId>(id => id.Value == invoiceId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        await _handler.Handle(command, CancellationToken.None);

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        invoice.Notes.Should().Be(reason);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenInvoiceNotFound_ThrowsNotFoundException()
    {
        var command = new CancelInvoiceCommand(Guid.NewGuid(), "Reason");

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<InvoiceId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice?)null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Invoice*");

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
