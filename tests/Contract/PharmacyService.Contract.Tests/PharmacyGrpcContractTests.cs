using AutoMapper;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using His.Hope.PharmacyGrpc;
using His.Hope.PharmacyService.Api.GrpcServices;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using Moq;

namespace His.Hope.PharmacyService.Contract.Tests;

public class PharmacyGrpcContractTests
{
    private readonly Mock<IMedicationRepository> _mockMedicationRepo;
    private readonly Mock<IPrescriptionRepository> _mockPrescriptionRepo;
    private readonly Mock<IMapper> _mockMapper;
    private readonly PharmacyGrpcServiceImpl _service;

    public PharmacyGrpcContractTests()
    {
        _mockMedicationRepo = new Mock<IMedicationRepository>();
        _mockPrescriptionRepo = new Mock<IPrescriptionRepository>();
        _mockMapper = new Mock<IMapper>();
        _service = new PharmacyGrpcServiceImpl(
            _mockMedicationRepo.Object, _mockPrescriptionRepo.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task GetMedication_WithValidId_ShouldReturnMedicationResponse()
    {
        var medicationId = MedicationId.New();
        var medication = CreateSampleMedication(medicationId);

        _mockMedicationRepo
            .Setup(r => r.GetByIdAsync(It.Is<MedicationId>(m => m.Value == medicationId.Value), It.IsAny<CancellationToken>()))
            .ReturnsAsync(medication);

        var request = new MedicationRequest { Id = medicationId.Value.ToString() };

        var response = await _service.GetMedication(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.Id.Should().Be(medicationId.Value.ToString());
        response.Name.Should().Be("Amoxicillin");
        response.GenericName.Should().Be("Amoxicillin Trihydrate");
        response.DosageForm.Should().Be("Capsule");
        response.Strength.Should().Be("500mg");
        response.RequiresPrescription.Should().BeTrue();
        response.IsActive.Should().BeTrue();
        response.CreatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMedication_WithNonExistentId_ShouldThrowNotFoundRpcException()
    {
        var nonExistentId = Guid.NewGuid();

        _mockMedicationRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<MedicationId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Medication?)null);

        var request = new MedicationRequest { Id = nonExistentId.ToString() };

        var act = async () => await _service.GetMedication(request, new TestServerCallContext());

        var exception = await act.Should().ThrowAsync<RpcException>();
        exception.And.StatusCode.Should().Be(StatusCode.NotFound);
        exception.And.Status.Detail.Should().Contain("not found");
    }

    [Fact]
    public async Task GetMedication_WithEmptyId_ShouldThrowRpcException()
    {
        var request = new MedicationRequest { Id = string.Empty };

        var act = async () => await _service.GetMedication(request, new TestServerCallContext());

        await act.Should().ThrowAsync<RpcException>();
    }

    [Fact]
    public async Task SearchMedications_WithValidSearchTerm_ShouldReturnListResponse()
    {
        var medications = new List<Medication>
        {
            CreateSampleMedication(MedicationId.New()),
            CreateSampleMedication(MedicationId.New())
        };

        _mockMedicationRepo
            .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((medications, 2));

        var request = new MedicationSearchRequest
        {
            SearchTerm = "amox",
            Page = 1,
            PageSize = 20
        };

        var response = await _service.SearchMedications(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.Medications.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(20);

        foreach (var m in response.Medications)
        {
            m.Id.Should().NotBeNullOrEmpty();
            m.Name.Should().NotBeNullOrEmpty();
            m.DosageForm.Should().NotBeNullOrEmpty();
            m.Strength.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task SearchMedications_WithNoResults_ShouldReturnEmptyList()
    {
        _mockMedicationRepo
            .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Medication>(), 0));

        var request = new MedicationSearchRequest { SearchTerm = "nonexistent" };

        var response = await _service.SearchMedications(request, new TestServerCallContext());

        response.Medications.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckMedicationExists_WithExistingId_ShouldReturnTrue()
    {
        _mockMedicationRepo
            .Setup(r => r.ExistsAsync(It.IsAny<MedicationId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new MedicationExistsRequest { Id = Guid.NewGuid().ToString() };

        var response = await _service.CheckMedicationExists(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.Exists.Should().BeTrue();
    }

    [Fact]
    public async Task CheckMedicationExists_WithNonExistentId_ShouldReturnFalse()
    {
        _mockMedicationRepo
            .Setup(r => r.ExistsAsync(It.IsAny<MedicationId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new MedicationExistsRequest { Id = Guid.NewGuid().ToString() };

        var response = await _service.CheckMedicationExists(request, new TestServerCallContext());

        response.Exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetPrescription_WithValidId_ShouldReturnPrescriptionResponse()
    {
        var prescriptionId = PrescriptionId.New();
        var prescription = CreateSamplePrescription(prescriptionId);

        _mockPrescriptionRepo
            .Setup(r => r.GetByIdAsync(It.Is<PrescriptionId>(p => p.Value == prescriptionId.Value), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prescription);

        var request = new PrescriptionRequest { Id = prescriptionId.Value.ToString() };

        var response = await _service.GetPrescription(request, new TestServerCallContext());

        response.Should().NotBeNull();
        response.Id.Should().Be(prescriptionId.Value.ToString());
        response.MedicationName.Should().Be("Amoxicillin");
        response.Strength.Should().Be("500mg");
        response.DosageForm.Should().Be("Capsule");
        response.Quantity.Should().Be(30);
        response.Refills.Should().Be(2);
        response.StatusCode.Should().Be("PRESCRIBED");
        response.StatusName.Should().Be("Prescribed");
        response.PrescribedAt.Should().NotBeNull();
        response.CreatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPrescription_WithNonExistentId_ShouldThrowNotFoundRpcException()
    {
        _mockPrescriptionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<PrescriptionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prescription?)null);

        var request = new PrescriptionRequest { Id = Guid.NewGuid().ToString() };

        var act = async () => await _service.GetPrescription(request, new TestServerCallContext());

        var exception = await act.Should().ThrowAsync<RpcException>();
        exception.And.StatusCode.Should().Be(StatusCode.NotFound);
        exception.And.Status.Detail.Should().Contain("not found");
    }

    [Fact]
    public async Task SearchPrescriptions_WithValidTerm_ShouldReturnListResponse()
    {
        var prescriptions = new List<Prescription>
        {
            CreateSamplePrescription(PrescriptionId.New()),
            CreateSamplePrescription(PrescriptionId.New())
        };

        _mockPrescriptionRepo
            .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((prescriptions, 2));

        var request = new PrescriptionSearchRequest { SearchTerm = "amox" };

        var response = await _service.SearchPrescriptions(request, new TestServerCallContext());

        response.Prescriptions.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task MedicationResponse_ProtoContract_HasAllRequiredFields()
    {
        var response = new MedicationResponse
        {
            Id = "med-id",
            Name = "Amoxicillin",
            GenericName = "Amoxicillin Trihydrate",
            BrandName = "Amoxil",
            DosageForm = "Capsule",
            Strength = "500mg",
            Route = "Oral",
            RequiresPrescription = true,
            IsActive = true
        };

        response.Id.Should().Be("med-id");
        response.Name.Should().Be("Amoxicillin");
        response.GenericName.Should().Be("Amoxicillin Trihydrate");
        response.BrandName.Should().Be("Amoxil");
        response.DosageForm.Should().Be("Capsule");
        response.Strength.Should().Be("500mg");
        response.Route.Should().Be("Oral");
        response.RequiresPrescription.Should().BeTrue();
        response.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task PrescriptionResponse_ProtoContract_HasAllRequiredFields()
    {
        var response = new PrescriptionResponse
        {
            Id = "rx-id",
            PatientId = "patient-id",
            ProviderId = "provider-id",
            MedicationId = "med-id",
            MedicationName = "Amoxicillin",
            Strength = "500mg",
            DosageForm = "Capsule",
            DosageInstructions = "Take one capsule three times daily",
            Route = "Oral",
            Quantity = 30,
            Refills = 2,
            StatusCode = "PRESCRIBED",
            StatusName = "Prescribed"
        };

        response.Id.Should().Be("rx-id");
        response.PatientId.Should().Be("patient-id");
        response.ProviderId.Should().Be("provider-id");
        response.MedicationName.Should().Be("Amoxicillin");
        response.DosageInstructions.Should().Be("Take one capsule three times daily");
        response.Quantity.Should().Be(30);
        response.Refills.Should().Be(2);
        response.StatusCode.Should().Be("PRESCRIBED");
    }

    [Fact]
    public async Task MedicationListResponse_ProtoContract_HasAllRequiredFields()
    {
        var listResponse = new MedicationListResponse();
        listResponse.Medications.Add(new MedicationResponse { Id = "1" });
        listResponse.Medications.Add(new MedicationResponse { Id = "2" });
        listResponse.TotalCount = 2;
        listResponse.Page = 1;
        listResponse.PageSize = 50;

        listResponse.Medications.Should().HaveCount(2);
        listResponse.TotalCount.Should().Be(2);
        listResponse.Page.Should().Be(1);
        listResponse.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task PrescriptionListResponse_ProtoContract_HasAllRequiredFields()
    {
        var listResponse = new PrescriptionListResponse();
        listResponse.Prescriptions.Add(new PrescriptionResponse { Id = "1" });
        listResponse.Prescriptions.Add(new PrescriptionResponse { Id = "2" });
        listResponse.TotalCount = 2;
        listResponse.Page = 1;
        listResponse.PageSize = 20;

        listResponse.Prescriptions.Should().HaveCount(2);
        listResponse.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task MedicationRequest_ProtoContract_AcceptsStringId()
    {
        var request = new MedicationRequest { Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890" };
        request.Id.Should().Be("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    }

    private static Medication CreateSampleMedication(MedicationId medicationId)
    {
        var med = Medication.Create("Amoxicillin", "Capsule", "500mg");
        var field = typeof(Medication).GetField("<GenericName>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(med, "Amoxicillin Trihydrate");
        var brandField = typeof(Medication).GetField("<BrandName>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        brandField?.SetValue(med, "Amoxil");
        var routeField = typeof(Medication).GetField("<Route>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        routeField?.SetValue(med, "Oral");
        return med;
    }

    private static Prescription CreateSamplePrescription(PrescriptionId prescriptionId)
    {
        return Prescription.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Amoxicillin", "500mg", "Capsule",
            "Take one capsule three times daily", "Oral",
            30, 2, null, DateTime.UtcNow.AddMonths(1));
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

    protected override string MethodCore => "/his.hope.pharmacy.PharmacyGrpcService/TestMethod";
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
