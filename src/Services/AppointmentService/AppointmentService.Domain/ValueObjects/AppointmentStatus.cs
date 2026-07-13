using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.AppointmentService.Domain.ValueObjects;

public class AppointmentStatus : Enumeration<AppointmentStatus>
{
    public static readonly AppointmentStatus Scheduled = new("SCHEDULED", "Scheduled");
    public static readonly AppointmentStatus CheckedIn = new("CHECKED_IN", "Checked In");
    public static readonly AppointmentStatus InProgress = new("IN_PROGRESS", "In Progress");
    public static readonly AppointmentStatus Completed = new("COMPLETED", "Completed");
    public static readonly AppointmentStatus Cancelled = new("CANCELLED", "Cancelled");
    public static readonly AppointmentStatus Rescheduled = new("RESCHEDULED", "Rescheduled");
    public static readonly AppointmentStatus NoShow = new("NO_SHOW", "No Show");

    private AppointmentStatus(string code, string name) : base(code, name) { }
}
