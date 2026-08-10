using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Commands;

public record DeactivatePatientCommand(Guid Id) : IRequest<Unit>;

public record ReactivatePatientCommand(Guid Id) : IRequest<Unit>;
