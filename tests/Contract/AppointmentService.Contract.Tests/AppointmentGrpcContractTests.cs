using FluentAssertions;
using Grpc.Core;
using Xunit;
using His.Hope.AppointmentGrpc;
using His.Hope.AppointmentService.Api.GrpcServices;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using Moq;

namespace His.Hope.AppointmentService.Contract.Tests;

public class AppointmentGrpcContractTests
{
    private readonly Mock<IAppointmentRepository> _mockRepo;
    private readonly AppointmentGrpcServiceImpl _service;

    public AppointmentGrpcContractTests()
    {
        _mockRepo = new Mock<IAppointmentRepository>();
        _service = new AppointmentGrpcServiceImpl(_mockRepo.Object);
    }

    [Fact]
    public async Task GetAppointment_WithValidId_ShouldReturnAppointmentResponse()
    {
        var appointmentId = AppointmentId.New();
        var appointment = CreateSampleAppointment(appointmentId);

        _mockRepo
            .Setup(r => r.GetByIdAsync(It.Is<AppointmentId>(a => a.Value == appointmentId.Value), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var request = new AppointmentRequest { Id = appointmentId.Value.ToString() };

        var response = await _service.GetAppointment(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.Id.Should().Be(appointmentId.Value.ToString());
        response.PatientId.Should().Be(appointment.PatientId.ToString());
        response.ProviderId.Should().Be(appointment.ProviderId.ToString());
        response.StatusCode.Should().Be("SCHEDULED");
        response.StatusName.Should().Be("Scheduled");
        response.TypeCode.Should().Be("CONSULT");
        response.ScheduledDate.Should().NotBeNull();
        response.StartTime.Should().NotBeNull();
        response.EndTime.Should().NotBeNull();
        response.CreatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAppointment_WithNonExistentId_ShouldThrowNotFoundRpcException()
    {
        _mockRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        var request = new AppointmentRequest { Id = Guid.NewGuid().ToString() };

        var act = async () => await _service.GetAppointment(request, new TestServerCallContext());

        var exception = await act.Should().ThrowAsync<RpcException>();
        exception.And.StatusCode.Should().Be(StatusCode.NotFound);
        exception.And.Status.Detail.Should().Contain("not found");
    }

    [Fact]
    public async Task GetAppointment_WithEmptyId_ShouldThrowRpcException()
    {
        var request = new AppointmentRequest { Id = string.Empty };

        var act = async () => await _service.GetAppointment(request, new TestServerCallContext());

        await act.Should().ThrowAsync<RpcException>();
    }

    [Fact]
    public async Task GetPatientAppointments_WithValidPatientId_ShouldReturnListResponse()
    {
        var patientId = Guid.NewGuid();
        var appointments = new List<Appointment>
        {
            CreateSampleAppointment(AppointmentId.New()),
            CreateSampleAppointment(AppointmentId.New())
        };

        _mockRepo
            .Setup(r => r.GetByPatientIdAsync(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointments);

        var request = new PatientAppointmentsRequest { PatientId = patientId.ToString() };

        var response = await _service.GetPatientAppointments(request, new TestServerCallContext());

        response.Appointments.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetPatientAppointments_WithNoResults_ShouldReturnEmptyList()
    {
        _mockRepo
            .Setup(r => r.GetByPatientIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());

        var request = new PatientAppointmentsRequest { PatientId = Guid.NewGuid().ToString() };

        var response = await _service.GetPatientAppointments(request, new TestServerCallContext());

        response.Appointments.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckAppointmentExists_WithExistingId_ShouldReturnTrue()
    {
        _mockRepo
            .Setup(r => r.ExistsAsync(It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new AppointmentExistsRequest { Id = Guid.NewGuid().ToString() };

        var response = await _service.CheckAppointmentExists(request, new TestServerCallContext());

        response.Exists.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAppointmentExists_WithNonExistentId_ShouldReturnFalse()
    {
        _mockRepo
            .Setup(r => r.ExistsAsync(It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new AppointmentExistsRequest { Id = Guid.NewGuid().ToString() };

        var response = await _service.CheckAppointmentExists(request, new TestServerCallContext());

        response.Exists.Should().BeFalse();
    }

    [Fact]
    public async Task AppointmentResponse_ProtoContract_HasAllRequiredFields()
    {
        var response = new AppointmentResponse
        {
            Id = "appt-id",
            PatientId = "patient-id",
            ProviderId = "provider-id",
            StatusCode = "SCHEDULED",
            StatusName = "Scheduled",
            TypeCode = "CONSULT"
        };

        response.Id.Should().Be("appt-id");
        response.PatientId.Should().Be("patient-id");
        response.ProviderId.Should().Be("provider-id");
        response.StatusCode.Should().Be("SCHEDULED");
        response.StatusName.Should().Be("Scheduled");
        response.TypeCode.Should().Be("CONSULT");
    }

    [Fact]
    public async Task AppointmentListResponse_ProtoContract_HasAllRequiredFields()
    {
        var listResponse = new AppointmentListResponse();
        listResponse.Appointments.Add(new AppointmentResponse { Id = "1" });
        listResponse.Appointments.Add(new AppointmentResponse { Id = "2" });
        listResponse.TotalCount = 2;
        listResponse.Page = 1;
        listResponse.PageSize = 20;

        listResponse.Appointments.Should().HaveCount(2);
        listResponse.TotalCount.Should().Be(2);
        listResponse.Page.Should().Be(1);
        listResponse.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task AppointmentExistsResponse_ProtoContract_HasExistsField()
    {
        var response = new AppointmentExistsResponse { Exists = true };
        response.Exists.Should().BeTrue();

        var falseResponse = new AppointmentExistsResponse { Exists = false };
        falseResponse.Exists.Should().BeFalse();
    }

    [Fact]
    public async Task AppointmentRequest_ProtoContract_AcceptsStringId()
    {
        var request = new AppointmentRequest { Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890" };
        request.Id.Should().Be("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    }

    [Fact]
    public async Task PatientAppointmentsRequest_ProtoContract_HasAllFields()
    {
        var request = new PatientAppointmentsRequest
        {
            PatientId = Guid.NewGuid().ToString(),
            Page = 1,
            PageSize = 10
        };

        request.PatientId.Should().NotBeNullOrEmpty();
        request.Page.Should().Be(1);
        request.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetAppointment_WithPastDate_ShouldHandleCorrectly()
    {
        var patientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var appointmentId = AppointmentId.New();
        var appointment = Appointment.Schedule(
            patientId, providerId, DateTime.Today.AddDays(1),
            new TimeSpan(9, 0, 0), 30,
            AppointmentType.Consultation, "Routine checkup", null);

        var idField = typeof(Appointment).GetProperty("Id");
        if (idField?.CanWrite == true)
            idField.SetValue(appointment, appointmentId);

        _mockRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<AppointmentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var request = new AppointmentRequest { Id = appointmentId.Value.ToString() };

        var response = await _service.GetAppointment(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.PatientId.Should().Be(patientId.ToString());
        response.ProviderId.Should().Be(providerId.ToString());
        response.TypeCode.Should().Be("CONSULT");
    }

    private static Appointment CreateSampleAppointment(AppointmentId appointmentId)
    {
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(1),
            new TimeSpan(9, 0, 0), 30,
            AppointmentType.Consultation, "Routine checkup", null);
        return appointment;
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

    protected override string MethodCore => "/his.hope.appointment.AppointmentGrpcService/TestMethod";
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
