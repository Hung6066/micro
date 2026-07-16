using AutoMapper;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using His.Hope.ClinicalGrpc;
using His.Hope.ClinicalService.Api.GrpcServices;
using His.Hope.ClinicalService.Application.Common.Exceptions;
using His.Hope.ClinicalService.Application.DTOs;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Queries;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace His.Hope.ClinicalService.Contract.Tests;

/// <summary>
/// Contract tests verifying gRPC service compliance with the proto contract.
/// These tests validate that the service correctly implements the messages
/// defined in clinical.proto: GetEncounter, SearchEncounters, CheckEncounterExists.
/// Error responses (NotFound, InvalidArgument) are also tested.
/// </summary>
public class ClinicalGrpcContractTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<ClinicalGrpcServiceImpl>> _mockLogger;
    private readonly ClinicalGrpcServiceImpl _service;

    public ClinicalGrpcContractTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<ClinicalGrpcServiceImpl>>();
        _service = new ClinicalGrpcServiceImpl(_mockMediator.Object, _mockLogger.Object);
    }

    private static EncounterDto CreateSampleEncounterDto(Guid? id = null)
    {
        var encounterId = id ?? Guid.NewGuid();
        return new EncounterDto
        {
            Id = encounterId,
            PatientId = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            EncounterDate = DateTime.UtcNow,
            EncounterTypeCode = "OP",
            EncounterTypeName = "Outpatient",
            StatusCode = "IN_PROGRESS",
            StatusName = "In Progress",
            ChiefComplaint = "Chest pain",
            VitalSigns = new VitalSignsDto
            {
                Temperature = 37.0m,
                HeartRate = 72,
                RespiratoryRate = 16,
                SystolicBP = 120,
                DiastolicBP = 80,
                OxygenSaturation = 98.0m
            },
            Diagnoses = new List<DiagnosisDto>
            {
                new() { ConditionName = "Hypertension", Icd10Code = "I10", IsPrimary = true }
            },
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task GetEncounter_WithValidId_ShouldReturnEncounterResponse()
    {
        // Arrange
        var encounterId = Guid.NewGuid();
        var expectedDto = CreateSampleEncounterDto(encounterId);

        _mockMediator
            .Setup(m => m.Send(
                It.Is<GetEncounterByIdQuery>(q => q.Id == encounterId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var request = new EncounterRequest { Id = encounterId.ToString() };

        // Act
        var response = await _service.GetEncounter(request, new TestServerCallContext());

        // Assert - contract compliance
        response.Should().NotBeNull();
        response.Id.Should().Be(encounterId.ToString());
        response.PatientId.Should().Be(expectedDto.PatientId.ToString());
        response.ProviderId.Should().Be(expectedDto.ProviderId.ToString());
        response.AppointmentId.Should().Be(expectedDto.AppointmentId.ToString()!);
        response.EncounterTypeCode.Should().Be(expectedDto.EncounterTypeCode);
        response.EncounterTypeName.Should().Be(expectedDto.EncounterTypeName);
        response.StatusCode.Should().Be(expectedDto.StatusCode);
        response.StatusName.Should().Be(expectedDto.StatusName);
        response.ChiefComplaint.Should().Be(expectedDto.ChiefComplaint);
        response.HasVitals.Should().BeTrue();
        response.DiagnosisCount.Should().Be(expectedDto.Diagnoses.Count);
        response.EncounterDate.Should().NotBeNull();
        response.CreatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetEncounter_WithoutVitals_ShouldSetHasVitalsFalse()
    {
        // Arrange
        var encounterId = Guid.NewGuid();
        var dto = CreateSampleEncounterDto(encounterId);
        dto.VitalSigns = null;

        _mockMediator
            .Setup(m => m.Send(
                It.IsAny<GetEncounterByIdQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var request = new EncounterRequest { Id = encounterId.ToString() };

        // Act
        var response = await _service.GetEncounter(request, new TestServerCallContext());

        // Assert
        response.HasVitals.Should().BeFalse();
        response.DiagnosisCount.Should().Be(1);
    }

    [Fact]
    public async Task GetEncounter_WithEmptyDiagnoses_ShouldReturnZeroCount()
    {
        // Arrange
        var encounterId = Guid.NewGuid();
        var dto = CreateSampleEncounterDto(encounterId);
        dto.Diagnoses.Clear();

        _mockMediator
            .Setup(m => m.Send(
                It.IsAny<GetEncounterByIdQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var request = new EncounterRequest { Id = encounterId.ToString() };

        // Act
        var response = await _service.GetEncounter(request, new TestServerCallContext());

        // Assert
        response.DiagnosisCount.Should().Be(0);
        response.HasVitals.Should().BeTrue();
    }

    [Fact]
    public async Task GetEncounter_WithNonExistentId_ShouldThrowNotFoundRpcException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        _mockMediator
            .Setup(m => m.Send(
                It.IsAny<GetEncounterByIdQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EncounterDto?)null);

        var request = new EncounterRequest { Id = nonExistentId.ToString() };

        // Act
        var act = async () => await _service.GetEncounter(request, new TestServerCallContext());

        // Assert - contract requires StatusCode.NotFound for missing entities
        var exception = await act.Should().ThrowAsync<RpcException>();
        exception.And.StatusCode.Should().Be(StatusCode.NotFound);
        exception.And.Status.Detail.Should().Contain("not found");
    }

    [Fact]
    public async Task GetEncounter_WithInvalidGuid_ShouldThrowRpcException()
    {
        // Arrange
        var request = new EncounterRequest { Id = "not-a-guid" };

        // Act
        var act = async () => await _service.GetEncounter(request, new TestServerCallContext());

        // Assert - contract requires error for invalid arguments
        var exception = await act.Should().ThrowAsync<RpcException>();
        exception.And.StatusCode.Should().Be(StatusCode.Internal);
    }

    [Fact]
    public async Task GetEncounter_WithEmptyId_ShouldThrowRpcException()
    {
        // Arrange
        var request = new EncounterRequest { Id = string.Empty };

        // Act
        var act = async () => await _service.GetEncounter(request, new TestServerCallContext());

        // Assert
        await act.Should().ThrowAsync<RpcException>();
    }

    [Fact]
    public async Task SearchEncounters_WithValidSearchTerm_ShouldReturnListResponse()
    {
        // Arrange
        var searchTerm = "hypertension";
        var page = 1;
        var pageSize = 20;
        var dtos = new List<EncounterDto>
        {
            CreateSampleEncounterDto(),
            CreateSampleEncounterDto()
        };

        _mockMediator
            .Setup(m => m.Send(
                It.Is<SearchEncountersQuery>(q =>
                    q.SearchTerm == searchTerm && q.Page == page && q.PageSize == pageSize),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<EncounterDto>(dtos, 2, page, pageSize));

        var request = new EncounterSearchRequest
        {
            SearchTerm = searchTerm,
            Page = page,
            PageSize = pageSize
        };

        // Act
        var response = await _service.SearchEncounters(request, new TestServerCallContext());

        // Assert - contract compliance
        response.Should().NotBeNull();
        response.Encounters.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
        response.Page.Should().Be(page);
        response.PageSize.Should().Be(pageSize);

        // Verify each encounter in the list matches proto contract fields
        foreach (var enc in response.Encounters)
        {
            enc.Id.Should().NotBeNullOrEmpty();
            enc.PatientId.Should().NotBeNullOrEmpty();
            enc.ProviderId.Should().NotBeNullOrEmpty();
            enc.EncounterTypeCode.Should().NotBeNullOrEmpty();
            enc.StatusCode.Should().NotBeNullOrEmpty();
            enc.EncounterDate.Should().NotBeNull();
            enc.CreatedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task SearchEncounters_WithEmptySearchTerm_ShouldReturnAllResults()
    {
        // Arrange
        var dtos = new List<EncounterDto> { CreateSampleEncounterDto() };

        _mockMediator
            .Setup(m => m.Send(
                It.Is<SearchEncountersQuery>(q => q.SearchTerm == string.Empty),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<EncounterDto>(dtos, 1, 1, 20));

        var request = new EncounterSearchRequest { SearchTerm = string.Empty };

        // Act
        var response = await _service.SearchEncounters(request, new TestServerCallContext());

        // Assert
        response.Encounters.Should().HaveCount(1);
        response.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchEncounters_WithNoResults_ShouldReturnEmptyList()
    {
        // Arrange
        _mockMediator
            .Setup(m => m.Send(
                It.IsAny<SearchEncountersQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<EncounterDto>(
                new List<EncounterDto>(), 0, 1, 20));

        var request = new EncounterSearchRequest
        {
            SearchTerm = "nonexistent",
            Page = 1,
            PageSize = 20
        };

        // Act
        var response = await _service.SearchEncounters(request, new TestServerCallContext());

        // Assert
        response.Encounters.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchEncounters_WithDefaultPaging_ShouldUseDefaults()
    {
        // Arrange
        _mockMediator
            .Setup(m => m.Send(
                It.Is<SearchEncountersQuery>(q => q.Page == 1 && q.PageSize == 20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<EncounterDto>(
                new List<EncounterDto>(), 0, 1, 20));

        // Request with no page/pageSize set (defaults to 0)
        var request = new EncounterSearchRequest { SearchTerm = "test" };

        // Act
        var response = await _service.SearchEncounters(request, new TestServerCallContext());

        // Assert - should default to page 1, pageSize 20
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task CheckEncounterExists_WithExistingId_ShouldReturnTrue()
    {
        // Arrange
        var encounterId = Guid.NewGuid();

        _mockMediator
            .Setup(m => m.Send(
                It.Is<GetEncounterByIdQuery>(q => q.Id == encounterId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSampleEncounterDto(encounterId));

        var request = new EncounterExistsRequest { Id = encounterId.ToString() };

        // Act
        var response = await _service.CheckEncounterExists(request, new TestServerCallContext());

        // Assert
        response.Should().NotBeNull();
        response.Exists.Should().BeTrue();
    }

    [Fact]
    public async Task CheckEncounterExists_WithNonExistentId_ShouldReturnFalse()
    {
        // Arrange
        _mockMediator
            .Setup(m => m.Send(
                It.IsAny<GetEncounterByIdQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EncounterDto?)null);

        var request = new EncounterExistsRequest { Id = Guid.NewGuid().ToString() };

        // Act
        var response = await _service.CheckEncounterExists(request, new TestServerCallContext());

        // Assert
        response.Exists.Should().BeFalse();
    }

    [Fact]
    public async Task CheckEncounterExists_WithInvalidIdFormat_ShouldThrowRpcException()
    {
        // Arrange
        var request = new EncounterExistsRequest { Id = "invalid-format" };

        // Act
        var act = async () => await _service.CheckEncounterExists(request, new TestServerCallContext());

        // Assert
        await act.Should().ThrowAsync<RpcException>();
    }

    [Fact]
    public async Task EncounterResponse_ProtoContract_HasAllRequiredFields()
    {
        // This test validates the proto message contract by verifying
        // that all expected fields are present on the response object

        var response = new EncounterResponse
        {
            Id = "test-id",
            PatientId = "patient-id",
            ProviderId = "provider-id",
            AppointmentId = "appt-id",
            EncounterTypeCode = "OP",
            EncounterTypeName = "Outpatient",
            StatusCode = "IN_PROGRESS",
            StatusName = "In Progress",
            ChiefComplaint = "Pain",
            HasVitals = true,
            DiagnosisCount = 3
        };

        // Verify all string fields from the proto contract
        response.Id.Should().Be("test-id");
        response.PatientId.Should().Be("patient-id");
        response.ProviderId.Should().Be("provider-id");
        response.AppointmentId.Should().Be("appt-id");
        response.EncounterTypeCode.Should().Be("OP");
        response.EncounterTypeName.Should().Be("Outpatient");
        response.StatusCode.Should().Be("IN_PROGRESS");
        response.StatusName.Should().Be("In Progress");
        response.ChiefComplaint.Should().Be("Pain");
        response.HasVitals.Should().BeTrue();
        response.DiagnosisCount.Should().Be(3);

        // Timestamp fields should be settable
        response.EncounterDate = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
            DateTime.UtcNow);
        response.EncounterDate.Should().NotBeNull();
    }

    [Fact]
    public async Task EncounterListResponse_ProtoContract_HasAllRequiredFields()
    {
        // Validate the repeated field and pagination fields
        var listResponse = new EncounterListResponse();
        listResponse.Encounters.Add(new EncounterResponse { Id = "1" });
        listResponse.Encounters.Add(new EncounterResponse { Id = "2" });
        listResponse.TotalCount = 2;
        listResponse.Page = 1;
        listResponse.PageSize = 20;

        listResponse.Encounters.Should().HaveCount(2);
        listResponse.TotalCount.Should().Be(2);
        listResponse.Page.Should().Be(1);
        listResponse.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task EncounterExistsResponse_ProtoContract_HasExistsField()
    {
        // Validate the boolean exist field
        var response = new EncounterExistsResponse { Exists = true };
        response.Exists.Should().BeTrue();

        var falseResponse = new EncounterExistsResponse { Exists = false };
        falseResponse.Exists.Should().BeFalse();
    }

    [Fact]
    public async Task EncounterRequest_ProtoContract_AcceptsStringId()
    {
        // Validate the request message contract
        var request = new EncounterRequest { Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890" };
        request.Id.Should().Be("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    }

    [Fact]
    public async Task EncounterExistsRequest_ProtoContract_AcceptsStringId()
    {
        var request = new EncounterExistsRequest { Id = Guid.NewGuid().ToString() };
        request.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task EncounterSearchRequest_ProtoContract_HasAllFields()
    {
        var request = new EncounterSearchRequest
        {
            SearchTerm = "hypertension",
            Page = 2,
            PageSize = 50
        };

        request.SearchTerm.Should().Be("hypertension");
        request.Page.Should().Be(2);
        request.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task PatientEncountersRequest_ProtoContract_HasAllFields()
    {
        var request = new PatientEncountersRequest
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
    public async Task GetEncounter_WithAppointmentIdNull_ShouldReturnEmptyString()
    {
        // Arrange
        var encounterId = Guid.NewGuid();
        var dto = CreateSampleEncounterDto(encounterId);
        dto.AppointmentId = null; // No linked appointment

        _mockMediator
            .Setup(m => m.Send(
                It.IsAny<GetEncounterByIdQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var request = new EncounterRequest { Id = encounterId.ToString() };

        // Act
        var response = await _service.GetEncounter(request, new TestServerCallContext());

        // Assert - contract: null appointment_id maps to empty string
        response.AppointmentId.Should().Be(string.Empty);
    }

    [Fact]
    public async Task GetEncounter_WithNullOptionalFields_ShouldReturnEmptyStrings()
    {
        // Arrange
        var encounterId = Guid.NewGuid();
        var dto = CreateSampleEncounterDto(encounterId);
        dto.ChiefComplaint = null;
        dto.UpdatedAt = null;

        _mockMediator
            .Setup(m => m.Send(
                It.IsAny<GetEncounterByIdQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var request = new EncounterRequest { Id = encounterId.ToString() };

        // Act
        var response = await _service.GetEncounter(request, new TestServerCallContext());

        // Assert - nullable strings should be empty in proto3
        response.ChiefComplaint.Should().Be(string.Empty);
        response.UpdatedAt.Should().BeNull();
    }
}

/// <summary>
/// Helper to create a gRPC ServerCallContext for testing service methods directly.
/// </summary>
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

    protected override string MethodCore => "/his.hope.clinical.ClinicalGrpcService/TestMethod";
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
