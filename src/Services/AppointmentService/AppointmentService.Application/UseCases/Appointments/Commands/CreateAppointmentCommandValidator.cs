using FluentValidation;
using His.Hope.AppointmentService.Domain.ValueObjects;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;

public class CreateAppointmentCommandValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentCommandValidator()
    {
        RuleFor(v => v.PatientId)
            .NotEmpty().WithMessage("Patient ID is required.");

        RuleFor(v => v.ProviderId)
            .NotEmpty().WithMessage("Provider ID is required.");

        RuleFor(v => v.ScheduledDate)
            .NotEmpty().WithMessage("Scheduled date is required.")
            .GreaterThanOrEqualTo(DateTime.Today).WithMessage("Scheduled date must be today or in the future.");

        RuleFor(v => v.StartTime)
            .NotEmpty().WithMessage("Start time is required.");

        RuleFor(v => v.DurationMinutes)
            .GreaterThan(0).WithMessage("Duration must be greater than 0 minutes.");

        RuleFor(v => v.TypeCode)
            .NotEmpty().WithMessage("Appointment type is required.")
            .Must(typeCode =>
            {
                try
                {
                    AppointmentType.FromCode(typeCode);
                    return true;
                }
                catch
                {
                    return false;
                }
            }).WithMessage("Invalid appointment type code.");

        RuleFor(v => v.Location)
            .NotEmpty().WithMessage("Location is required.");
    }
}
