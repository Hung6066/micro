using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.AppointmentService.Domain.ValueObjects;

public class AppointmentType : Enumeration<AppointmentType>
{
    public static readonly AppointmentType Checkup = new("CHECKUP", "General Checkup");
    public static readonly AppointmentType Consultation = new("CONSULT", "Consultation");
    public static readonly AppointmentType FollowUp = new("FOLLOWUP", "Follow-up Visit");
    public static readonly AppointmentType Emergency = new("EMERG", "Emergency Visit");
    public static readonly AppointmentType Procedure = new("PROCED", "Procedure");
    public static readonly AppointmentType Vaccination = new("VACCINE", "Vaccination");
    public static readonly AppointmentType LabWork = new("LAB", "Lab Work");
    public static readonly AppointmentType Telehealth = new("TELE", "Telehealth Visit");

    private AppointmentType(string code, string name) : base(code, name) { }
}
