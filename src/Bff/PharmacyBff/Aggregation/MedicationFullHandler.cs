using His.Hope.Bff.Core.Aggregation;
using His.Hope.PharmacyGrpc;

namespace PharmacyBff.Aggregation;

public sealed class MedicationFullHandler : IAggregationHandler
{
    public string Route => "/api/v1/medications/{id}/full";
    public string Method => "GET";

    private readonly PharmacyGrpcService.PharmacyGrpcServiceClient _pharmacyClient;

    public MedicationFullHandler(PharmacyGrpcService.PharmacyGrpcServiceClient pharmacyClient)
    {
        _pharmacyClient = pharmacyClient;
    }

    public async Task<AggregationResult> HandleAsync(AggregationContext context)
    {
        var medId = context.RouteValues["id"]!;
        var results = await ParallelAggregationExecutor.RunAsync(new()
        {
            ["medication"] = () => _pharmacyClient.GetMedicationAsync(
                new MedicationRequest { Id = medId }, cancellationToken: context.CancellationToken)
                .ResponseAsync.ContinueWith(t => (object)t.Result),
            ["prescriptions"] = () => _pharmacyClient.SearchPrescriptionsAsync(
                new PrescriptionSearchRequest { SearchTerm = medId }, cancellationToken: context.CancellationToken)
                .ResponseAsync.ContinueWith(t => (object)t.Result)
        });

        return results.Successes.Count > 0
            ? AggregationResult.Partial(new { data = results.Successes }, results.Failures)
            : AggregationResult.Failed("PharmacyService unavailable");
    }
}
