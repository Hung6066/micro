using Hl7.Fhir.Model;

namespace His.Hope.FhirGateway.Application.Adapters;

/// <summary>
/// Maps His.Hope domain Encounter data to HL7 FHIR R4 Encounter resources.
/// </summary>
public static class EncounterFhirAdapter
{
    /// <summary>
    /// Converts domain encounter fields into a FHIR Encounter resource.
    /// </summary>
    /// <param name="id">The His.Hope encounter identifier.</param>
    /// <param name="patientId">The His.Hope patient identifier (becomes the Subject reference).</param>
    /// <param name="statusCode">Encounter status code (e.g. IN_PROGRESS, FINISHED).</param>
    /// <param name="classCode">Encounter class code (e.g. AMB, OBS, IMP).</param>
    /// <param name="className">Display name for the class.</param>
    /// <param name="periodStart">Start time of the encounter.</param>
    /// <param name="periodEnd">Optional end time of the encounter.</param>
    /// <returns>A populated FHIR Encounter resource.</returns>
    public static Encounter ToFhir(
        string id,
        string patientId,
        string statusCode,
        string classCode,
        string? className,
        DateTimeOffset periodStart,
        DateTimeOffset? periodEnd)
    {
        var encounter = new Encounter
        {
            Id = id,
            Identifier = new List<Identifier>
            {
                new()
                {
                    System = "https://his.hope.vn/encounter-id",
                    Value = id
                }
            },
            Status = MapStatus(statusCode),
            Class = new Coding
            {
                System = "http://terminology.hl7.org/CodeSystem/v3-ActCode",
                Code = classCode,
                Display = className ?? classCode
            },
            Subject = new ResourceReference($"Patient/{patientId}"),
            Period = new Period
            {
                Start = periodStart.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                End = periodEnd?.ToString("yyyy-MM-ddTHH:mm:sszzz")
            }
        };

        return encounter;
    }

    private static Encounter.EncounterStatus MapStatus(string statusCode)
    {
        return statusCode?.ToUpperInvariant() switch
        {
            "PLANNED" or "SCHEDULED" => Encounter.EncounterStatus.Planned,
            "IN_PROGRESS" or "ACTIVE" => Encounter.EncounterStatus.InProgress,
            "ON_HOLD" or "PAUSED" => Encounter.EncounterStatus.Onleave,
            "DISCHARGED" or "FINISHED" or "COMPLETED" => Encounter.EncounterStatus.Finished,
            "CANCELLED" or "CANCELED" => Encounter.EncounterStatus.Cancelled,
            "ENTERED_IN_ERROR" => Encounter.EncounterStatus.EnteredInError,
            _ => Encounter.EncounterStatus.Unknown
        };
    }
}
