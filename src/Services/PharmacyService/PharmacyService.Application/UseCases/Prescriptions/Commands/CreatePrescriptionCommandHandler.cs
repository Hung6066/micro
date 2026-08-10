using AutoMapper;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Commands;

public class CreatePrescriptionCommandHandler : IRequestHandler<CreatePrescriptionCommand, PrescriptionDto>
{
    private readonly IPrescriptionRepository _prescriptionRepository;
    private readonly IMapper _mapper;
    private readonly DomainEventDispatcher _eventDispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePrescriptionCommandHandler(
        IPrescriptionRepository prescriptionRepository,
        IMapper mapper,
        DomainEventDispatcher eventDispatcher)
    {
        _prescriptionRepository = prescriptionRepository;
        _mapper = mapper;
        _eventDispatcher = eventDispatcher;
        _unitOfWork = prescriptionRepository.UnitOfWork;
    }

    public async Task<PrescriptionDto> Handle(CreatePrescriptionCommand request,
        CancellationToken cancellationToken)
    {
        var prescription = Prescription.Create(
            request.PatientId,
            request.ProviderId,
            request.MedicationId,
            request.MedicationName,
            request.Strength,
            request.DosageForm,
            request.DosageInstructions,
            request.Route,
            request.Quantity,
            request.Refills,
            request.Notes,
            request.ExpiryDate);

        await _prescriptionRepository.AddAsync(prescription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<PrescriptionDto>(prescription);
    }
}
