using AutoMapper;
using His.Hope.PharmacyService.Application.Common.Exceptions;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.PharmacyService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Medications.Commands;

public class UpdateMedicationCommandHandler : IRequestHandler<UpdateMedicationCommand, MedicationDto>
{
    private readonly IMedicationRepository _medicationRepository;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateMedicationCommandHandler(
        IMedicationRepository medicationRepository,
        IMapper mapper)
    {
        _medicationRepository = medicationRepository;
        _mapper = mapper;
        _unitOfWork = medicationRepository.UnitOfWork;
    }

    public async Task<MedicationDto> Handle(UpdateMedicationCommand request,
        CancellationToken cancellationToken)
    {
        var medicationId = MedicationId.From(request.Id);
        var medication = await _medicationRepository.GetByIdAsync(medicationId, cancellationToken);

        if (medication is null)
            throw new NotFoundException(nameof(Medication), request.Id);

        medication.UpdateDetails(
            request.Name,
            request.GenericName,
            request.BrandName,
            request.DosageForm,
            request.Strength,
            request.Route,
            request.Category,
            request.Manufacturer,
            request.RequiresPrescription);

        await _medicationRepository.UpdateAsync(medication, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<MedicationDto>(medication);
    }
}
