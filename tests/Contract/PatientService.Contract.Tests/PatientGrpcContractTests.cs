using AutoMapper;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using His.Hope.PatientGrpc;
using His.Hope.PatientService.Api.GrpcServices;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;
using Moq;

namespace His.Hope.PatientService.Contract.Tests;

public class PatientGrpcContractTests
{
    private readonly Mock<IPatientRepository> _mockRepo;
    private readonly Mock<IMapper> _mockMapper;
    private readonly PatientGrpcServiceImpl _service;

    public PatientGrpcContractTests()
    {
        _mockRepo = new Mock<IPatientRepository>();
        _mockMapper = new Mock<IMapper>();
        _service = new PatientGrpcServiceImpl(_mockRepo.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task GetPatient_WithValidId_ShouldReturnPatientResponse()
    {
        var patientId = PatientId.New();
        var patient = CreateSamplePatient(patientId);

        _mockRepo
            .Setup(r => r.GetByIdAsync(It.Is<PatientId>(p => p.Value == patientId.Value), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var request = new PatientRequest { Id = patientId.Value.ToString() };

        var response = await _service.GetPatient(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.Id.Should().Be(patientId.Value.ToString());
        response.FullName.Should().Be("John Michael Doe");
        response.FirstName.Should().Be("John");
        response.LastName.Should().Be("Doe");
        response.MiddleName.Should().Be("Michael");
        response.GenderCode.Should().Be("M");
        response.GenderName.Should().Be("Male");
        response.Phone.Should().Be("+1234567890");
        response.Email.Should().Be("john.doe@example.com");
        response.IsActive.Should().BeTrue();
        response.DateOfBirth.Should().NotBeNull();
        response.CreatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPatient_WithNonExistentId_ShouldThrowNotFoundRpcException()
    {
        var nonExistentId = Guid.NewGuid();

        _mockRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<PatientId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var request = new PatientRequest { Id = nonExistentId.ToString() };

        var act = async () => await _service.GetPatient(request, new TestServerCallContext());

        var exception = await act.Should().ThrowAsync<RpcException>();
        exception.And.StatusCode.Should().Be(StatusCode.NotFound);
        exception.And.Status.Detail.Should().Contain("not found");
    }

    [Fact]
    public async Task GetPatient_WithEmptyId_ShouldThrowRpcException()
    {
        var request = new PatientRequest { Id = string.Empty };

        var act = async () => await _service.GetPatient(request, new TestServerCallContext());

        await act.Should().ThrowAsync<RpcException>();
    }

    [Fact]
    public async Task SearchPatients_WithValidSearchTerm_ShouldReturnListResponse()
    {
        var patients = new List<Patient>
        {
            CreateSamplePatient(PatientId.New()),
            CreateSamplePatient(PatientId.New())
        };

        _mockRepo
            .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((patients, 2));

        var request = new PatientSearchRequest
        {
            SearchTerm = "John",
            Page = 1,
            PageSize = 20
        };

        var response = await _service.SearchPatients(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.Patients.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(20);

        foreach (var p in response.Patients)
        {
            p.Id.Should().NotBeNullOrEmpty();
            p.FullName.Should().NotBeNullOrEmpty();
            p.GenderCode.Should().NotBeNullOrEmpty();
            p.Phone.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task SearchPatients_WithNoResults_ShouldReturnEmptyList()
    {
        _mockRepo
            .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Patient>(), 0));

        var request = new PatientSearchRequest { SearchTerm = "nonexistent" };

        var response = await _service.SearchPatients(request, new TestServerCallContext());

        response.Patients.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckPatientExists_WithExistingId_ShouldReturnTrue()
    {
        var patientId = PatientId.New();

        _mockRepo
            .Setup(r => r.ExistsAsync(It.IsAny<PatientId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new PatientExistsRequest { Id = patientId.Value.ToString() };

        var response = await _service.CheckPatientExists(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.Exists.Should().BeTrue();
    }

    [Fact]
    public async Task CheckPatientExists_WithNonExistentId_ShouldReturnFalse()
    {
        _mockRepo
            .Setup(r => r.ExistsAsync(It.IsAny<PatientId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new PatientExistsRequest { Id = Guid.NewGuid().ToString() };

        var response = await _service.CheckPatientExists(request, new TestServerCallContext());

        response.Exists.Should().BeFalse();
    }

    [Fact]
    public async Task PatientResponse_ProtoContract_HasAllRequiredFields()
    {
        var response = new PatientResponse
        {
            Id = "test-id",
            FullName = "John Michael Doe",
            FirstName = "John",
            LastName = "Doe",
            MiddleName = "Michael",
            GenderCode = "M",
            GenderName = "Male",
            Phone = "+1234567890",
            Email = "john@example.com",
            IsActive = true
        };

        response.Id.Should().Be("test-id");
        response.FullName.Should().Be("John Michael Doe");
        response.FirstName.Should().Be("John");
        response.LastName.Should().Be("Doe");
        response.MiddleName.Should().Be("Michael");
        response.GenderCode.Should().Be("M");
        response.GenderName.Should().Be("Male");
        response.Phone.Should().Be("+1234567890");
        response.Email.Should().Be("john@example.com");
        response.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task PatientListResponse_ProtoContract_HasAllRequiredFields()
    {
        var listResponse = new PatientListResponse();
        listResponse.Patients.Add(new PatientResponse { Id = "1" });
        listResponse.Patients.Add(new PatientResponse { Id = "2" });
        listResponse.TotalCount = 2;
        listResponse.Page = 1;
        listResponse.PageSize = 20;

        listResponse.Patients.Should().HaveCount(2);
        listResponse.TotalCount.Should().Be(2);
        listResponse.Page.Should().Be(1);
        listResponse.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task PatientExistsResponse_ProtoContract_HasExistsField()
    {
        var response = new PatientExistsResponse { Exists = true };
        response.Exists.Should().BeTrue();

        var falseResponse = new PatientExistsResponse { Exists = false };
        falseResponse.Exists.Should().BeFalse();
    }

    [Fact]
    public async Task PatientRequest_ProtoContract_AcceptsStringId()
    {
        var request = new PatientRequest { Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890" };
        request.Id.Should().Be("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    }

    [Fact]
    public async Task PatientSearchRequest_ProtoContract_HasAllFields()
    {
        var request = new PatientSearchRequest
        {
            SearchTerm = "Smith",
            Page = 2,
            PageSize = 50
        };

        request.SearchTerm.Should().Be("Smith");
        request.Page.Should().Be(2);
        request.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task GetPatient_WithNullEmail_ShouldReturnEmptyString()
    {
        var patientId = PatientId.New();
        var patient = CreateSamplePatient(patientId);
        var field = typeof(ContactInfo).GetField("<Email>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(patient.ContactInfo, null);

        _mockRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<PatientId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var request = new PatientRequest { Id = patientId.Value.ToString() };

        var response = await _service.GetPatient(request, new TestServerCallContext());

        response.Email.Should().Be(string.Empty);
    }

    private static Patient CreateSamplePatient(PatientId patientId)
    {
        return Patient.Register(
            PersonName.Create("John", "Michael", "Doe"),
            new DateTime(1990, 5, 15),
            PatientService.Domain.ValueObjects.Gender.Male,
            ContactInfo.Create("+1234567890", "john.doe@example.com"),
            Address.Create("123 Main St", "", "Springfield", "IL", "62701", "USA"));
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

    protected override string MethodCore => "/his.hope.patient.PatientGrpcService/TestMethod";
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
