using His.Hope.PharmacyService.Application.Common.Exceptions;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Commands;

public record FillPrescriptionCommand(Guid Id) : IRequest<Unit>;

public class FillPrescriptionCommandHandler : IRequestHandler<FillPrescriptionCommand, Unit>
{
    private readonly IPrescriptionRepository _prescriptionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public FillPrescriptionCommandHandler(IPrescriptionRepository prescriptionRepository)
    {
        _prescriptionRepository = prescriptionRepository;
        _unitOfWork = prescriptionRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(FillPrescriptionCommand request,
        CancellationToken cancellationToken)
    {
        var prescriptionId = PrescriptionId.From(request.Id);
        var prescription = await _prescriptionRepository.GetByIdAsync(prescriptionId, cancellationToken);

        if (prescription is null)
            throw new NotFoundException(nameof(Prescription), request.Id);

        prescription.Fill();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
