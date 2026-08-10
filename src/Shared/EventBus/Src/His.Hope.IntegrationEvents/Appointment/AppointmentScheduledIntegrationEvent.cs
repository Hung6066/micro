using His.Hope.EventBus.Abstractions;

namespace His.Hope.IntegrationEvents.Appointment;

public class AppointmentScheduledIntegrationEvent : IntegrationEvent
{
    public Guid AppointmentId { get; }
    public Guid PatientId { get; }
    public Guid ProviderId { get; }
    public DateTime ScheduledDate { get; }
    public TimeSpan StartTime { get; }
    public TimeSpan EndTime { get; }

    public AppointmentScheduledIntegrationEvent(
        Guid appointmentId, Guid patientId, Guid providerId,
        DateTime scheduledDate, TimeSpan startTime, TimeSpan endTime)
    {
        AppointmentId = appointmentId;
        PatientId = patientId;
        ProviderId = providerId;
        ScheduledDate = scheduledDate;
        StartTime = startTime;
        EndTime = endTime;
    }
}
