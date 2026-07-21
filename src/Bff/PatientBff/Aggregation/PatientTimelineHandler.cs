using His.Hope.Bff.Core.Aggregation;
using His.Hope.PatientGrpc;
using His.Hope.ClinicalGrpc;
using His.Hope.LabGrpc;
using His.Hope.PharmacyGrpc;
using Polly;
using Polly.Registry;

namespace PatientBff.Aggregation;

public sealed class PatientTimelineHandler : IAggregationHandler
{
    public string Route => "/api/v1/patients/{id}/timeline";
    public string Method => "GET";

    private readonly PatientGrpcService.PatientGrpcServiceClient _patientClient;
    private readonly ClinicalGrpcService.ClinicalGrpcServiceClient _clinicalClient;
    private readonly LabGrpcService.LabGrpcServiceClient _labClient;
    private readonly PharmacyGrpcService.PharmacyGrpcServiceClient _pharmacyClient;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<PatientTimelineHandler> _logger;

    public PatientTimelineHandler(
        PatientGrpcService.PatientGrpcServiceClient patientClient,
        ClinicalGrpcService.ClinicalGrpcServiceClient clinicalClient,
        LabGrpcService.LabGrpcServiceClient labClient,
        PharmacyGrpcService.PharmacyGrpcServiceClient pharmacyClient,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<PatientTimelineHandler> logger)
    {
        _patientClient = patientClient;
        _clinicalClient = clinicalClient;
        _labClient = labClient;
        _pharmacyClient = pharmacyClient;
        _pipeline = pipelineProvider.GetPipeline("bff-downstream");
        _logger = logger;
    }

    public async Task<AggregationResult> HandleAsync(AggregationContext context)
    {
        var patientId = context.RouteValues["id"]!;
        var ct = context.CancellationToken;

        var results = await ParallelAggregationExecutor.RunAsync(new()
        {
            ["patient"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching patient {PatientId}", patientId);
                var resp = await _patientClient.GetPatientAsync(
                    new PatientRequest { Id = patientId }, cancellationToken: ct);
                return (object)resp;
            }, ct).AsTask(),
            ["encounters"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching encounters for patient {PatientId}", patientId);
                var resp = await _clinicalClient.GetPatientEncountersAsync(
                    new PatientEncountersRequest { PatientId = patientId }, cancellationToken: ct);
                return (object)resp;
            }, ct).AsTask(),
            ["labOrders"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching lab orders for patient {PatientId}", patientId);
                var resp = await _labClient.GetPatientLabOrdersAsync(
                    new PatientLabOrdersRequest { PatientId = patientId }, cancellationToken: ct);
                return (object)resp;
            }, ct).AsTask(),
            ["prescriptions"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching prescriptions for patient {PatientId}", patientId);
                var resp = await _pharmacyClient.SearchPrescriptionsAsync(
                    new PrescriptionSearchRequest { PatientId = patientId }, cancellationToken: ct);
                return (object)resp;
            }, ct).AsTask()
        });

        return results.Successes.Count > 0
            ? AggregationResult.Partial(new { data = results.Successes }, results.Failures)
            : AggregationResult.Failed("All downstream services unavailable");
    }
}
