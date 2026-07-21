using Grpc.Core;
using His.Hope.Bff.Core.Aggregation;
using PatientBff.Aggregation;
using His.Hope.PatientGrpc;
using His.Hope.ClinicalGrpc;
using His.Hope.LabGrpc;
using His.Hope.PharmacyGrpc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Polly;
using Polly.Registry;
using Xunit;

namespace PatientBff.Tests.Aggregation;

public class PatientTimelineHandlerTests
{
    private readonly PatientGrpcService.PatientGrpcServiceClient _patientClient;
    private readonly ClinicalGrpcService.ClinicalGrpcServiceClient _clinicalClient;
    private readonly LabGrpcService.LabGrpcServiceClient _labClient;
    private readonly PharmacyGrpcService.PharmacyGrpcServiceClient _pharmacyClient;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly ILogger<PatientTimelineHandler> _logger;
    private readonly PatientTimelineHandler _handler;

    public PatientTimelineHandlerTests()
    {
        _patientClient = Substitute.For<PatientGrpcService.PatientGrpcServiceClient>();
        _clinicalClient = Substitute.For<ClinicalGrpcService.ClinicalGrpcServiceClient>();
        _labClient = Substitute.For<LabGrpcService.LabGrpcServiceClient>();
        _pharmacyClient = Substitute.For<PharmacyGrpcService.PharmacyGrpcServiceClient>();

        var pipeline = new ResiliencePipelineBuilder().Build();
        _pipelineProvider = Substitute.For<ResiliencePipelineProvider<string>>();
        _pipelineProvider.GetPipeline("bff-downstream").Returns(pipeline);

        _logger = Substitute.For<ILogger<PatientTimelineHandler>>();

        _handler = new PatientTimelineHandler(
            _patientClient, _clinicalClient, _labClient, _pharmacyClient,
            _pipelineProvider, _logger);
    }

    [Fact]
    public async Task PartialFailure_Returns200WithDegraded()
    {
        var patientId = "p001";
        _patientClient.GetPatientAsync(Arg.Any<PatientRequest>(), null, null, default)
            .Returns(CreateCall(new PatientResponse { Id = patientId, FullName = "John Doe" }));
        _clinicalClient.GetPatientEncountersAsync(Arg.Any<PatientEncountersRequest>(), null, null, default)
            .Returns(x => throw new RpcException(new Status(StatusCode.DeadlineExceeded, "timeout")));
        _labClient.GetPatientLabOrdersAsync(Arg.Any<PatientLabOrdersRequest>(), null, null, default)
            .Returns(CreateCall(new LabOrderListResponse()));
        _pharmacyClient.SearchPrescriptionsAsync(Arg.Any<PrescriptionSearchRequest>(), null, null, default)
            .Returns(CreateCall(new PrescriptionListResponse()));

        var context = new AggregationContext(
            new Dictionary<string, string> { ["id"] = patientId },
            "jwt", default);

        var result = await _handler.HandleAsync(context);

        Assert.Equal(200, result.StatusCode);
        Assert.Contains(result.Degraded, d => d.Field == "encounters");
    }

    [Fact]
    public async Task AllDownstreamsFail_Returns502()
    {
        var patientId = "p001";
        _patientClient.GetPatientAsync(Arg.Any<PatientRequest>(), null, null, default)
            .Returns(x => throw new RpcException(new Status(StatusCode.Unavailable, "unavailable")));
        _clinicalClient.GetPatientEncountersAsync(Arg.Any<PatientEncountersRequest>(), null, null, default)
            .Returns(x => throw new RpcException(new Status(StatusCode.Unavailable, "unavailable")));
        _labClient.GetPatientLabOrdersAsync(Arg.Any<PatientLabOrdersRequest>(), null, null, default)
            .Returns(x => throw new RpcException(new Status(StatusCode.Unavailable, "unavailable")));
        _pharmacyClient.SearchPrescriptionsAsync(Arg.Any<PrescriptionSearchRequest>(), null, null, default)
            .Returns(x => throw new RpcException(new Status(StatusCode.Unavailable, "unavailable")));

        var context = new AggregationContext(
            new Dictionary<string, string> { ["id"] = patientId },
            "jwt", default);

        var result = await _handler.HandleAsync(context);

        Assert.Equal(502, result.StatusCode);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task FullSuccess_Returns200WithAllData()
    {
        var patientId = "p001";
        _patientClient.GetPatientAsync(Arg.Any<PatientRequest>(), null, null, default)
            .Returns(CreateCall(new PatientResponse { Id = patientId, FullName = "John Doe" }));
        _clinicalClient.GetPatientEncountersAsync(Arg.Any<PatientEncountersRequest>(), null, null, default)
            .Returns(CreateCall(new EncounterListResponse()));
        _labClient.GetPatientLabOrdersAsync(Arg.Any<PatientLabOrdersRequest>(), null, null, default)
            .Returns(CreateCall(new LabOrderListResponse()));
        _pharmacyClient.SearchPrescriptionsAsync(Arg.Any<PrescriptionSearchRequest>(), null, null, default)
            .Returns(CreateCall(new PrescriptionListResponse()));

        var context = new AggregationContext(
            new Dictionary<string, string> { ["id"] = patientId },
            "jwt", default);

        var result = await _handler.HandleAsync(context);

        Assert.Equal(200, result.StatusCode);
        Assert.Empty(result.Degraded);
    }

    private static AsyncUnaryCall<T> CreateCall<T>(T response)
    {
        return new AsyncUnaryCall<T>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }
}
