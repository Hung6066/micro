using His.Hope.PharmacyService.Application.Common.Exceptions;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Commands;

public record CancelPrescriptionCommand(Guid Id, string Reason) : IRequest<Unit>;

public class CancelPrescriptionCommandHandler : IRequestHandler<CancelPrescriptionCommand, Unit>
{
    private readonly IPrescriptionRepository _prescriptionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPrescriptionCommandHandler(IPrescriptionRepository prescriptionRepository)
    {
        _prescriptionRepository = prescriptionRepository;
        _unitOfWork = prescriptionRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(CancelPrescriptionCommand request,
        CancellationToken cancellationToken)
    {
        var prescriptionId = PrescriptionId.From(request.Id);
        var prescription = await _prescriptionRepository.GetByIdAsync(prescriptionId, cancellationToken);

        if (prescription is null)
            throw new NotFoundException(nameof(Prescription), request.Id);

        prescription.Cancel(request.Reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
