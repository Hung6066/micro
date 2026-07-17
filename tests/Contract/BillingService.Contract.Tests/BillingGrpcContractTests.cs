using AutoMapper;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using His.Hope.BillingGrpc;
using His.Hope.BillingService.Api.GrpcServices;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.BillingService.Application.DTOs;
using His.Hope.BillingService.Application.UseCases.Invoices.Queries;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace His.Hope.BillingService.Contract.Tests;

public class BillingGrpcContractTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IInvoiceRepository> _mockRepo;
    private readonly Mock<IMapper> _mockMapper;
    private readonly BillingGrpcServiceImpl _service;

    public BillingGrpcContractTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockRepo = new Mock<IInvoiceRepository>();
        _mockMapper = new Mock<IMapper>();
        _service = new BillingGrpcServiceImpl(_mockMediator.Object, _mockRepo.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task GetInvoice_WithValidId_ShouldReturnInvoiceResponse()
    {
        var invoiceId = InvoiceId.New();
        var invoice = CreateSampleInvoice(invoiceId, addLineItem: true);

        _mockRepo
            .Setup(r => r.GetByIdAsync(It.Is<InvoiceId>(i => i.Value == invoiceId.Value), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var request = new InvoiceRequest { Id = invoiceId.Value.ToString() };

        var response = await _service.GetInvoice(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.Id.Should().Be(invoiceId.Value.ToString());
        response.InvoiceNumber.Should().Be("INV-001");
        response.PatientId.Should().Be(invoice.PatientId.ToString());
        response.StatusCode.Should().Be("DRAFT");
        response.StatusName.Should().Be("Draft");
        response.SubTotal.Should().Be(150.0);
        response.TotalAmount.Should().Be(150.0);
        response.BalanceDue.Should().Be(150.0);
        response.InvoiceDate.Should().NotBeNull();
        response.CreatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetInvoice_WithNonExistentId_ShouldThrowNotFoundRpcException()
    {
        var nonExistentId = Guid.NewGuid();

        _mockRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<InvoiceId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice?)null);

        var request = new InvoiceRequest { Id = nonExistentId.ToString() };

        var act = async () => await _service.GetInvoice(request, new TestServerCallContext());

        var exception = await act.Should().ThrowAsync<RpcException>();
        exception.And.StatusCode.Should().Be(StatusCode.NotFound);
        exception.And.Status.Detail.Should().Contain("not found");
    }

    [Fact]
    public async Task GetInvoice_WithEmptyId_ShouldThrowRpcException()
    {
        var request = new InvoiceRequest { Id = string.Empty };

        var act = async () => await _service.GetInvoice(request, new TestServerCallContext());

        await act.Should().ThrowAsync<RpcException>();
    }

    [Fact]
    public async Task GetInvoiceByNumber_ExistingNumber_ShouldReturnInvoiceResponse()
    {
        var invoice = CreateSampleInvoice(InvoiceId.New());

        _mockRepo
            .Setup(r => r.GetByInvoiceNumberAsync("INV-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var request = new InvoiceByNumberRequest { InvoiceNumber = "INV-001" };

        var response = await _service.GetInvoiceByNumber(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.InvoiceNumber.Should().Be("INV-001");
    }

    [Fact]
    public async Task CheckInvoiceExists_WithExistingId_ShouldReturnTrue()
    {
        var invoice = CreateSampleInvoice(InvoiceId.New());

        _mockRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<InvoiceId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var request = new InvoiceExistsRequest { Id = Guid.NewGuid().ToString() };

        var response = await _service.CheckInvoiceExists(request, new TestServerCallContext());

        response.Exists.Should().BeTrue();
    }

    [Fact]
    public async Task CheckInvoiceExists_WithNonExistentId_ShouldReturnFalse()
    {
        _mockRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<InvoiceId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice?)null);

        var request = new InvoiceExistsRequest { Id = Guid.NewGuid().ToString() };

        var response = await _service.CheckInvoiceExists(request, new TestServerCallContext());

        response.Exists.Should().BeFalse();
    }

    [Fact]
    public async Task SearchInvoices_WithValidTerm_ShouldReturnListResponse()
    {
        var invoices = new List<Invoice>
        {
            CreateSampleInvoice(InvoiceId.New()),
            CreateSampleInvoice(InvoiceId.New())
        };

        _mockRepo
            .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedInvoiceResult(invoices, 2));

        var request = new InvoiceSearchRequest { SearchTerm = "INV" };

        var response = await _service.SearchInvoices(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.Invoices.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPatientInvoices_WithValidPatientId_ShouldReturnListResponse()
    {
        var patientId = Guid.NewGuid();
        var invoices = new List<Invoice>
        {
            CreateSampleInvoice(InvoiceId.New()),
            CreateSampleInvoice(InvoiceId.New())
        };

        _mockRepo
            .Setup(r => r.GetByPatientAsync(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);

        var request = new PatientInvoicesRequest { PatientId = patientId.ToString() };

        var response = await _service.GetPatientInvoices(request, new TestServerCallContext());

        response.Invoices.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task InvoiceResponse_ProtoContract_HasAllRequiredFields()
    {
        var response = new InvoiceResponse
        {
            Id = "inv-id",
            PatientId = "patient-id",
            EncounterId = "enc-id",
            InvoiceNumber = "INV-001",
            StatusCode = "DRAFT",
            StatusName = "Draft",
            SubTotal = 150.0,
            TaxAmount = 15.0,
            DiscountAmount = 0.0,
            TotalAmount = 165.0,
            PaidAmount = 0.0,
            BalanceDue = 165.0
        };

        response.Id.Should().Be("inv-id");
        response.PatientId.Should().Be("patient-id");
        response.EncounterId.Should().Be("enc-id");
        response.InvoiceNumber.Should().Be("INV-001");
        response.StatusCode.Should().Be("DRAFT");
        response.StatusName.Should().Be("Draft");
        response.SubTotal.Should().Be(150.0);
        response.TaxAmount.Should().Be(15.0);
        response.TotalAmount.Should().Be(165.0);
        response.BalanceDue.Should().Be(165.0);
    }

    [Fact]
    public async Task InvoiceListResponse_ProtoContract_HasAllRequiredFields()
    {
        var listResponse = new InvoiceListResponse();
        listResponse.Invoices.Add(new InvoiceResponse { Id = "1" });
        listResponse.Invoices.Add(new InvoiceResponse { Id = "2" });
        listResponse.TotalCount = 2;
        listResponse.Page = 1;
        listResponse.PageSize = 20;

        listResponse.Invoices.Should().HaveCount(2);
        listResponse.TotalCount.Should().Be(2);
        listResponse.Page.Should().Be(1);
        listResponse.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task InvoiceLineItemResponse_ProtoContract_HasAllFields()
    {
        var item = new InvoiceLineItemResponse
        {
            Id = "li-id",
            Description = "Consultation",
            Quantity = 1,
            UnitPrice = 150.0,
            Amount = 150.0,
            ItemCode = "CONS-001",
            ItemTypeCode = "SERVICE",
            ItemTypeName = "Service"
        };

        item.Description.Should().Be("Consultation");
        item.Quantity.Should().Be(1);
        item.UnitPrice.Should().Be(150.0);
        item.Amount.Should().Be(150.0);
    }

    [Fact]
    public async Task PaymentResponse_ProtoContract_HasAllFields()
    {
        var payment = new PaymentResponse
        {
            Id = "pay-id",
            Amount = 100.0,
            MethodCode = "CASH",
            MethodName = "Cash",
            StatusCode = "COMPLETED",
            StatusName = "Completed"
        };

        payment.Amount.Should().Be(100.0);
        payment.MethodCode.Should().Be("CASH");
        payment.StatusCode.Should().Be("COMPLETED");
    }

    [Fact]
    public async Task InvoiceRequest_ProtoContract_AcceptsStringId()
    {
        var request = new InvoiceRequest { Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890" };
        request.Id.Should().Be("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    }

    private static Invoice CreateSampleInvoice(InvoiceId invoiceId, bool addLineItem = false)
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(), null, "INV-001",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), "Test invoice");

        if (addLineItem)
        {
            invoice.AddLineItem(InvoiceLineItem.Create(
                invoice.Id, "Consultation", 1, 150.00m,
                "CONS-001", InvoiceLineItemType.Consultation));
        }

        return invoice;
    }
}

public class TestServerCallContext : ServerCallContext
{
    private readonly Metadata _requestHeaders;
    private readonly CancellationToken _cancellationToken;
    private readonly Metadata _responseTrailers;
    private readonly AuthContext _authContext;
    private readonly Dictionary<object, object> _userState;

    public TestServerCallContext()
    {
        _requestHeaders = new Metadata();
        _cancellationToken = CancellationToken.None;
        _responseTrailers = new Metadata();
        _authContext = new AuthContext(string.Empty, new Dictionary<string, List<AuthProperty>>());
        _userState = new Dictionary<object, object>();
    }

    protected override string MethodCore => "/his.hope.billing.BillingGrpcService/TestMethod";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "ipv4:127.0.0.1:5000";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => _requestHeaders;
    protected override CancellationToken CancellationTokenCore => _cancellationToken;
    protected override Metadata ResponseTrailersCore => _responseTrailers;
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => _authContext;

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => throw new NotImplementedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => Task.CompletedTask;

    protected override IDictionary<object, object> UserStateCore => _userState;
}
