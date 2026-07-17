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

public class AddInvoiceLineItemCommandHandlerTests
{
    private readonly Mock<IInvoiceRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly AddInvoiceLineItemCommandHandler _handler;

    public AddInvoiceLineItemCommandHandlerTests()
    {
        _mockRepository = new Mock<IInvoiceRepository>();
        _mockMapper = new Mock<IMapper>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _mockRepository.Setup(r => r.UnitOfWork).Returns(_mockUnitOfWork.Object);

        _handler = new AddInvoiceLineItemCommandHandler(
            _mockRepository.Object,
            _mockMapper.Object);
    }

    private Invoice CreateDraftInvoice()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(), Guid.NewGuid(), "INV-001",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), null);
        invoice.AddLineItem(InvoiceLineItem.Create(
            invoice.Id, "Existing item", 1, 100m, null, InvoiceLineItemType.Service));
        return invoice;
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsLineItemAndReturnsDto()
    {
        var invoice = CreateDraftInvoice();
        var invoiceId = invoice.Id.Value;

        var command = new AddInvoiceLineItemCommand(
            InvoiceId: invoiceId,
            Description: "New lab test",
            Quantity: 2,
            UnitPrice: 75.00m,
            ItemCode: "LAB001",
            ItemTypeCode: "LAB");

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
        invoice.LineItems.Should().HaveCount(2);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenInvoiceNotFound_ThrowsNotFoundException()
    {
        var command = new AddInvoiceLineItemCommand(
            InvoiceId: Guid.NewGuid(),
            Description: "Item",
            Quantity: 1,
            UnitPrice: 100m,
            ItemCode: null,
            ItemTypeCode: null);

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
    public async Task Handle_WithoutItemType_AddsLineItem()
    {
        var invoice = CreateDraftInvoice();
        var invoiceId = invoice.Id.Value;

        var command = new AddInvoiceLineItemCommand(
            InvoiceId: invoiceId,
            Description: "Generic item",
            Quantity: 1,
            UnitPrice: 50.00m,
            ItemCode: null,
            ItemTypeCode: null);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<InvoiceId>(id => id.Value == invoiceId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>()))
            .Returns(new InvoiceDto());

        await _handler.Handle(command, CancellationToken.None);

        invoice.LineItems.Should().HaveCount(2);
        invoice.LineItems.Last().ItemCode.Should().BeNull();
        invoice.LineItems.Last().ItemType.Should().BeNull();

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
