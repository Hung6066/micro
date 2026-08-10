using His.Hope.Bff.Core.Aggregation;
using His.Hope.ClinicalGrpc;

namespace ClinicalBff.Aggregation;

public class EncounterVitalsHandler : IAggregationHandler
{
    private readonly ClinicalGrpcService.ClinicalGrpcServiceClient _clinicalClient;

    public string Route => "/api/v1/encounters/{id}/vitals";
    public string Method => "GET";

    public EncounterVitalsHandler(ClinicalGrpcService.ClinicalGrpcServiceClient clinicalClient)
    {
        _clinicalClient = clinicalClient;
    }

    public async Task<AggregationResult> HandleAsync(AggregationContext context)
    {
        var encounterId = context.RouteValues["id"]!;

        var encounter = await _clinicalClient.GetEncounterAsync(
            new EncounterRequest { Id = encounterId },
            cancellationToken: context.CancellationToken).ResponseAsync;

        return AggregationResult.Success(new
        {
            encounter_id = encounter.Id,
            patient_id = encounter.PatientId,
            has_vitals = encounter.HasVitals,
            diagnosis_count = encounter.DiagnosisCount,
            encounter_date = encounter.EncounterDate,
            encounter_type = encounter.EncounterTypeCode,
            status = encounter.StatusCode,
            chief_complaint = encounter.ChiefComplaint
        });
    }
}
