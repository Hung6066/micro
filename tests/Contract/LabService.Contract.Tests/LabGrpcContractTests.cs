using AutoMapper;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using His.Hope.LabGrpc;
using His.Hope.LabService.Api.GrpcServices;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using Moq;

namespace His.Hope.LabService.Contract.Tests;

public class LabGrpcContractTests
{
    private readonly Mock<ILabOrderRepository> _mockRepo;
    private readonly Mock<IMapper> _mockMapper;
    private readonly LabGrpcServiceImpl _service;

    public LabGrpcContractTests()
    {
        _mockRepo = new Mock<ILabOrderRepository>();
        _mockMapper = new Mock<IMapper>();
        _service = new LabGrpcServiceImpl(_mockRepo.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task GetLabOrder_WithValidId_ShouldReturnLabOrderResponse()
    {
        var labOrderId = LabOrderId.New();
        var labOrder = CreateSampleLabOrder(labOrderId, addTest: true);

        _mockRepo
            .Setup(r => r.GetByIdAsync(It.Is<LabOrderId>(o => o.Value == labOrderId.Value), It.IsAny<CancellationToken>()))
            .ReturnsAsync(labOrder);

        var request = new LabOrderRequest { Id = labOrderId.Value.ToString() };

        var response = await _service.GetLabOrder(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.Id.Should().Be(labOrderId.Value.ToString());
        response.PatientId.Should().Be(labOrder.PatientId.ToString());
        response.ProviderId.Should().Be(labOrder.ProviderId.ToString());
        response.StatusCode.Should().Be("PENDING");
        response.StatusName.Should().Be("Pending");
        response.PriorityCode.Should().Be("ROUTINE");
        response.PriorityName.Should().Be("Routine");
        response.OrderDate.Should().NotBeNull();
    }

    [Fact]
    public async Task GetLabOrder_WithNonExistentId_ShouldThrowNotFoundRpcException()
    {
        var nonExistentId = Guid.NewGuid();

        _mockRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabOrder?)null);

        var request = new LabOrderRequest { Id = nonExistentId.ToString() };

        var act = async () => await _service.GetLabOrder(request, new TestServerCallContext());

        var exception = await act.Should().ThrowAsync<RpcException>();
        exception.And.StatusCode.Should().Be(StatusCode.NotFound);
        exception.And.Status.Detail.Should().Contain("not found");
    }

    [Fact]
    public async Task GetLabOrder_WithEmptyId_ShouldThrowRpcException()
    {
        var request = new LabOrderRequest { Id = string.Empty };

        var act = async () => await _service.GetLabOrder(request, new TestServerCallContext());

        await act.Should().ThrowAsync<RpcException>();
    }

    [Fact]
    public async Task GetPatientLabOrders_WithValidPatientId_ShouldReturnListResponse()
    {
        var patientId = Guid.NewGuid();
        var orders = new List<LabOrder>
        {
            CreateSampleLabOrder(LabOrderId.New()),
            CreateSampleLabOrder(LabOrderId.New())
        };

        _mockRepo
            .Setup(r => r.GetByPatientAsync(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

        var request = new PatientLabOrdersRequest { PatientId = patientId.ToString() };

        var response = await _service.GetPatientLabOrders(request, new TestServerCallContext());

        response.LabOrders.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task CheckLabOrderExists_WithExistingId_ShouldReturnTrue()
    {
        var labOrder = CreateSampleLabOrder(LabOrderId.New());

        _mockRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(labOrder);

        var request = new LabOrderExistsRequest { Id = Guid.NewGuid().ToString() };

        var response = await _service.CheckLabOrderExists(request, new TestServerCallContext());

        response.Exists.Should().BeTrue();
    }

    [Fact]
    public async Task CheckLabOrderExists_WithNonExistentId_ShouldReturnFalse()
    {
        _mockRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LabOrder?)null);

        var request = new LabOrderExistsRequest { Id = Guid.NewGuid().ToString() };

        var response = await _service.CheckLabOrderExists(request, new TestServerCallContext());

        response.Exists.Should().BeFalse();
    }

    [Fact]
    public async Task SearchLabOrders_WithValidTerm_ShouldReturnListResponse()
    {
        var orders = new List<LabOrder>
        {
            CreateSampleLabOrder(LabOrderId.New()),
            CreateSampleLabOrder(LabOrderId.New())
        };

        _mockRepo
            .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((orders, 2));

        var request = new LabOrderSearchRequest { SearchTerm = "blood" };

        var response = await _service.SearchLabOrders(request, new TestServerCallContext());

        response.LabOrders.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task SearchLabOrders_WithNoResults_ShouldReturnEmptyList()
    {
        _mockRepo
            .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<LabOrder>(), 0));

        var request = new LabOrderSearchRequest { SearchTerm = "nonexistent" };

        var response = await _service.SearchLabOrders(request, new TestServerCallContext());

        response.LabOrders.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task LabOrderResponse_ProtoContract_HasAllRequiredFields()
    {
        var response = new LabOrderResponse
        {
            Id = "lab-id",
            PatientId = "patient-id",
            ProviderId = "provider-id",
            EncounterId = "enc-id",
            StatusCode = "SUBMITTED",
            StatusName = "Submitted",
            PriorityCode = "URGENT",
            PriorityName = "Urgent",
            Notes = "Fasting required"
        };

        response.Id.Should().Be("lab-id");
        response.PatientId.Should().Be("patient-id");
        response.ProviderId.Should().Be("provider-id");
        response.EncounterId.Should().Be("enc-id");
        response.StatusCode.Should().Be("SUBMITTED");
        response.StatusName.Should().Be("Submitted");
        response.PriorityCode.Should().Be("URGENT");
        response.Notes.Should().Be("Fasting required");
    }

    [Fact]
    public async Task LabTestResponse_ProtoContract_HasAllFields()
    {
        var testResponse = new LabTestResponse
        {
            Id = "test-id",
            TestCode = "CBC",
            TestName = "Complete Blood Count",
            SpecimenType = "Blood",
            StatusCode = "ORDERED",
            StatusName = "Ordered"
        };

        testResponse.TestCode.Should().Be("CBC");
        testResponse.TestName.Should().Be("Complete Blood Count");
        testResponse.SpecimenType.Should().Be("Blood");
        testResponse.StatusCode.Should().Be("ORDERED");
    }

    [Fact]
    public async Task LabResultResponse_ProtoContract_HasAllFields()
    {
        var result = new LabResultResponse
        {
            LabResultId = "result-id",
            Value = "12.5",
            Unit = "g/dL",
            ReferenceRange = "13.5-17.5",
            AbnormalFlagCode = "ABNORMAL",
            AbnormalFlagName = "Abnormal",
            ResultStatusCode = "FINAL",
            ResultStatusName = "Final",
            PerformedBy = "Dr. Smith"
        };

        result.Value.Should().Be("12.5");
        result.Unit.Should().Be("g/dL");
        result.AbnormalFlagCode.Should().Be("ABNORMAL");
        result.ResultStatusCode.Should().Be("FINAL");
    }

    [Fact]
    public async Task LabOrderListResponse_ProtoContract_HasAllRequiredFields()
    {
        var listResponse = new LabOrderListResponse();
        listResponse.LabOrders.Add(new LabOrderResponse { Id = "1" });
        listResponse.LabOrders.Add(new LabOrderResponse { Id = "2" });
        listResponse.TotalCount = 2;
        listResponse.Page = 1;
        listResponse.PageSize = 20;

        listResponse.LabOrders.Should().HaveCount(2);
        listResponse.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task LabOrderRequest_ProtoContract_AcceptsStringId()
    {
        var request = new LabOrderRequest { Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890" };
        request.Id.Should().Be("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    }

    private static LabOrder CreateSampleLabOrder(LabOrderId labOrderId, bool addTest = false)
    {
        var labOrder = LabOrder.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            LabOrderPriority.Routine, "Routine blood work");

        if (addTest)
        {
            var test = LabTest.Create(labOrder.Id, "CBC", "Complete Blood Count", "Blood");
            labOrder.AddTest(test);
        }

        return labOrder;
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

    protected override string MethodCore => "/his.hope.lab.LabGrpcService/TestMethod";
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
