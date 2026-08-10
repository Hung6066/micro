using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Commands;

public class DeactivatePatientCommandHandler : IRequestHandler<DeactivatePatientCommand, Unit>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivatePatientCommandHandler(IPatientRepository patientRepository)
    {
        _patientRepository = patientRepository;
        _unitOfWork = patientRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(DeactivatePatientCommand request,
        CancellationToken cancellationToken)
    {
        var patientId = PatientId.From(request.Id);
        var patient = await _patientRepository.GetByIdAsync(patientId, cancellationToken);

        if (patient is null)
            throw new NotFoundException(nameof(Patient), request.Id);

        patient.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
