using His.Hope.EventBus.Abstractions;

namespace His.Hope.IntegrationEvents.Clinical;

public class EncounterStartedIntegrationEvent : IntegrationEvent
{
    public Guid EncounterId { get; }
    public Guid PatientId { get; }
    public Guid ProviderId { get; }
    public Guid? AppointmentId { get; }
    public string EncounterTypeCode { get; }
    public DateTime EncounterDate { get; }

    public EncounterStartedIntegrationEvent(
        Guid encounterId, Guid patientId, Guid providerId,
        Guid? appointmentId, string encounterTypeCode, DateTime encounterDate)
    {
        EncounterId = encounterId;
        PatientId = patientId;
        ProviderId = providerId;
        AppointmentId = appointmentId;
        EncounterTypeCode = encounterTypeCode;
        EncounterDate = encounterDate;
    }
}
