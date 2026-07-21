using His.Hope.Bff.Core.Aggregation;
using His.Hope.PatientGrpc;
using His.Hope.ClinicalGrpc;
using His.Hope.LabGrpc;
using His.Hope.BillingGrpc;
using His.Hope.PharmacyGrpc;
using Polly;
using Polly.Registry;

namespace DashboardBff.Aggregation;

public sealed class DashboardStatsHandler : IAggregationHandler
{
    public string Route => "/api/v1/dashboard/stats";
    public string Method => "GET";

    private readonly PatientGrpcService.PatientGrpcServiceClient _patientClient;
    private readonly ClinicalGrpcService.ClinicalGrpcServiceClient _clinicalClient;
    private readonly LabGrpcService.LabGrpcServiceClient _labClient;
    private readonly BillingGrpcService.BillingGrpcServiceClient _billingClient;
    private readonly PharmacyGrpcService.PharmacyGrpcServiceClient _pharmacyClient;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<DashboardStatsHandler> _logger;

    public DashboardStatsHandler(
        PatientGrpcService.PatientGrpcServiceClient patientClient,
        ClinicalGrpcService.ClinicalGrpcServiceClient clinicalClient,
        LabGrpcService.LabGrpcServiceClient labClient,
        BillingGrpcService.BillingGrpcServiceClient billingClient,
        PharmacyGrpcService.PharmacyGrpcServiceClient pharmacyClient,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<DashboardStatsHandler> logger)
    {
        _patientClient = patientClient;
        _clinicalClient = clinicalClient;
        _labClient = labClient;
        _billingClient = billingClient;
        _pharmacyClient = pharmacyClient;
        _pipeline = pipelineProvider.GetPipeline("bff-downstream");
        _logger = logger;
    }

    public async Task<AggregationResult> HandleAsync(AggregationContext context)
    {
        var ct = context.CancellationToken;

        var results = await ParallelAggregationExecutor.RunAsync(new()
        {
            ["totalPatients"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching total patient count");
                var resp = await _patientClient.SearchPatientsAsync(
                    new PatientSearchRequest(), cancellationToken: ct);
                return (object)new { count = resp.TotalCount };
            }, ct).AsTask(),

            ["activeEncounters"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching encounter count");
                var resp = await _clinicalClient.SearchEncountersAsync(
                    new EncounterSearchRequest(), cancellationToken: ct);
                return (object)new { count = resp.TotalCount };
            }, ct).AsTask(),

            ["pendingLabs"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching lab order count");
                var resp = await _labClient.SearchLabOrdersAsync(
                    new LabOrderSearchRequest(), cancellationToken: ct);
                return (object)new { count = resp.TotalCount };
            }, ct).AsTask(),

            ["outstandingInvoices"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching invoice count");
                var resp = await _billingClient.SearchInvoicesAsync(
                    new InvoiceSearchRequest(), cancellationToken: ct);
                return (object)new { count = resp.TotalCount };
            }, ct).AsTask(),

            ["medications"] = () => _pipeline.ExecuteAsync(async ct =>
            {
                _logger.LogDebug("Fetching medication count");
                var resp = await _pharmacyClient.SearchMedicationsAsync(
                    new MedicationSearchRequest(), cancellationToken: ct);
                return (object)new { count = resp.TotalCount };
            }, ct).AsTask()
        });

        return results.Successes.Count > 0
            ? AggregationResult.Partial(new { stats = results.Successes }, results.Failures)
            : AggregationResult.Failed("All downstream services unavailable");
    }
}
