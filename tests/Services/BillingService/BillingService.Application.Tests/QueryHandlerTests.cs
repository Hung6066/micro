using AutoMapper;
using FluentAssertions;
using His.Hope.BillingService.Application.DTOs;
using His.Hope.BillingService.Application.UseCases.Invoices.Queries;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using Moq;
using Xunit;

namespace His.Hope.BillingService.Application.Tests;

public class QueryHandlerTests
{
    private readonly Mock<IInvoiceRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;

    public QueryHandlerTests()
    {
        _mockRepository = new Mock<IInvoiceRepository>();
        _mockMapper = new Mock<IMapper>();
    }

    private Invoice CreateTestInvoice()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(), Guid.NewGuid(), "INV-001",
            DateTime.UtcNow, null, null);
        invoice.AddLineItem(InvoiceLineItem.Create(
            invoice.Id, "Item", 1, 100m, null, InvoiceLineItemType.Service));
        return invoice;
    }

    [Fact]
    public async Task GetById_WithExistingInvoice_ReturnsDto()
    {
        var invoice = CreateTestInvoice();
        var invoiceId = invoice.Id.Value;

        var query = new GetInvoiceByIdQuery(invoiceId);

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.Is<InvoiceId>(id => id.Value == invoiceId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var expectedDto = new InvoiceDto { Id = invoiceId, InvoiceNumber = "INV-001" };
        _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>()))
            .Returns(expectedDto);

        var handler = new GetInvoiceByIdQueryHandler(_mockRepository.Object, _mockMapper.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);
    }

    [Fact]
    public async Task GetById_WithNonExistingInvoice_ReturnsNull()
    {
        var query = new GetInvoiceByIdQuery(Guid.NewGuid());

        _mockRepository.Setup(r => r.GetByIdAsync(
                It.IsAny<InvoiceId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice?)null);

        var handler = new GetInvoiceByIdQueryHandler(_mockRepository.Object, _mockMapper.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByPatient_ReturnsMappedInvoices()
    {
        var patientId = Guid.NewGuid();
        var invoices = new List<Invoice> { CreateTestInvoice(), CreateTestInvoice() };

        var query = new GetInvoicesByPatientQuery(patientId);

        _mockRepository.Setup(r => r.GetByPatientAsync(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);

        var expectedDtos = new List<InvoiceDto>
        {
            new() { Id = Guid.NewGuid() },
            new() { Id = Guid.NewGuid() }
        };
        _mockMapper.Setup(m => m.Map<List<InvoiceDto>>(It.IsAny<List<Invoice>>()))
            .Returns(expectedDtos);

        var handler = new GetInvoicesByPatientQueryHandler(_mockRepository.Object, _mockMapper.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(expectedDtos);
    }

    [Fact]
    public async Task Search_ReturnsPagedResult()
    {
        var invoices = new List<Invoice> { CreateTestInvoice() };
        var pagedResult = new PagedInvoiceResult(invoices, 1);

        var query = new SearchInvoicesQuery(
            Term: "INV",
            Page: 1,
            PageSize: 20,
            PatientId: null,
            Status: null,
            DateFrom: null,
            DateTo: null);

        _mockRepository.Setup(r => r.SearchAsync(
                query.Term, query.Page, query.PageSize,
                query.PatientId, query.Status, query.DateFrom, query.DateTo,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var expectedDtos = new List<InvoiceDto>
        {
            new() { Id = Guid.NewGuid(), InvoiceNumber = "INV-001" }
        };
        _mockMapper.Setup(m => m.Map<List<InvoiceDto>>(It.IsAny<List<Invoice>>()))
            .Returns(expectedDtos);

        var handler = new SearchInvoicesQueryHandler(_mockRepository.Object, _mockMapper.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task Search_WithMultiplePages_HasCorrectPagination()
    {
        var invoices = new List<Invoice> { CreateTestInvoice(), CreateTestInvoice() };
        var pagedResult = new PagedInvoiceResult(invoices, 25);

        var query = new SearchInvoicesQuery(
            Term: "",
            Page: 2,
            PageSize: 10,
            PatientId: null,
            Status: null,
            DateFrom: null,
            DateTo: null);

        _mockRepository.Setup(r => r.SearchAsync(
                query.Term, query.Page, query.PageSize,
                query.PatientId, query.Status, query.DateFrom, query.DateTo,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var expectedDtos = new List<InvoiceDto>
        {
            new() { Id = Guid.NewGuid() },
            new() { Id = Guid.NewGuid() }
        };
        _mockMapper.Setup(m => m.Map<List<InvoiceDto>>(It.IsAny<List<Invoice>>()))
            .Returns(expectedDtos);

        var handler = new SearchInvoicesQueryHandler(_mockRepository.Object, _mockMapper.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        result.Page.Should().Be(2);
        result.TotalCount.Should().Be(25);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task GetByInvoiceNumber_WithExistingInvoice_ReturnsDto()
    {
        var invoice = CreateTestInvoice();
        const string invoiceNumber = "INV-001";

        var query = new GetInvoiceByNumberQuery(invoiceNumber);

        _mockRepository.Setup(r => r.GetByInvoiceNumberAsync(
                invoiceNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var expectedDto = new InvoiceDto { Id = invoice.Id.Value, InvoiceNumber = invoiceNumber };
        _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>()))
            .Returns(expectedDto);

        var handler = new GetInvoiceByNumberQueryHandler(_mockRepository.Object, _mockMapper.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Be(expectedDto);
    }

    [Fact]
    public async Task GetByInvoiceNumber_WithNonExistingInvoice_ReturnsNull()
    {
        var query = new GetInvoiceByNumberQuery("NONEXISTENT");

        _mockRepository.Setup(r => r.GetByInvoiceNumberAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice?)null);

        var handler = new GetInvoiceByNumberQueryHandler(_mockRepository.Object, _mockMapper.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().BeNull();
    }
}
