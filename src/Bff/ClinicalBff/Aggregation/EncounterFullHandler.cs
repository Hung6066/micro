using His.Hope.Bff.Core.Aggregation;
using His.Hope.ClinicalGrpc;

namespace ClinicalBff.Aggregation;

public class EncounterFullHandler : IAggregationHandler
{
    private readonly ClinicalGrpcService.ClinicalGrpcServiceClient _clinicalClient;

    public string Route => "/api/v1/encounters/{id}/full";
    public string Method => "GET";

    public EncounterFullHandler(ClinicalGrpcService.ClinicalGrpcServiceClient clinicalClient)
    {
        _clinicalClient = clinicalClient;
    }

    public async Task<AggregationResult> HandleAsync(AggregationContext context)
    {
        var encounterId = context.RouteValues["id"]!;

        var encounter = await _clinicalClient.GetEncounterAsync(
            new EncounterRequest { Id = encounterId },
            cancellationToken: context.CancellationToken).ResponseAsync;

        var tasks = new Dictionary<string, Func<Task<object>>>
        {
            ["encounter"] = () => Task.FromResult<object>(encounter),
            ["encounters"] = async () => await _clinicalClient.GetPatientEncountersAsync(
                new PatientEncountersRequest { PatientId = encounter.PatientId },
                cancellationToken: context.CancellationToken).ResponseAsync
        };

        var results = await ParallelAggregationExecutor.RunAsync(tasks, context.CancellationToken);

        return results.Successes.Count > 0
            ? AggregationResult.Partial(new { data = results.Successes }, results.Failures)
            : AggregationResult.Failed("ClinicalService unavailable");
    }
}
