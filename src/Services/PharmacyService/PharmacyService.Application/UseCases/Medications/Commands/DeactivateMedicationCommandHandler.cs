using His.Hope.PharmacyService.Application.Common.Exceptions;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Medications.Commands;

public record DeactivateMedicationCommand(Guid Id) : IRequest<Unit>;

public record ReactivateMedicationCommand(Guid Id) : IRequest<Unit>;

public class DeactivateMedicationCommandHandler : IRequestHandler<DeactivateMedicationCommand, Unit>
{
    private readonly IMedicationRepository _medicationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateMedicationCommandHandler(IMedicationRepository medicationRepository)
    {
        _medicationRepository = medicationRepository;
        _unitOfWork = medicationRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(DeactivateMedicationCommand request,
        CancellationToken cancellationToken)
    {
        var medicationId = MedicationId.From(request.Id);
        var medication = await _medicationRepository.GetByIdAsync(medicationId, cancellationToken);

        if (medication is null)
            throw new NotFoundException(nameof(Medication), request.Id);

        medication.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public class ReactivateMedicationCommandHandler : IRequestHandler<ReactivateMedicationCommand, Unit>
{
    private readonly IMedicationRepository _medicationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivateMedicationCommandHandler(IMedicationRepository medicationRepository)
    {
        _medicationRepository = medicationRepository;
        _unitOfWork = medicationRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(ReactivateMedicationCommand request,
        CancellationToken cancellationToken)
    {
        var medicationId = MedicationId.From(request.Id);
        var medication = await _medicationRepository.GetByIdAsync(medicationId, cancellationToken);

        if (medication is null)
            throw new NotFoundException(nameof(Medication), request.Id);

        medication.Reactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
