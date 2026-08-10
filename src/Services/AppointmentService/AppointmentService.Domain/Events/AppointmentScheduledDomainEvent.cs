using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.AppointmentService.Domain.Events;

public sealed record AppointmentScheduledDomainEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid ProviderId,
    DateTime ScheduledDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    DateTime OccurredOn) : IDomainEvent;
