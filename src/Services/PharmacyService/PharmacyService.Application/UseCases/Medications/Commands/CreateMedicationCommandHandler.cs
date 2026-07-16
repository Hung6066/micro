using AutoMapper;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Medications.Commands;

public class CreateMedicationCommandHandler : IRequestHandler<CreateMedicationCommand, MedicationDto>
{
    private readonly IMedicationRepository _medicationRepository;
    private readonly IMapper _mapper;
    private readonly DomainEventDispatcher _eventDispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateMedicationCommandHandler(
        IMedicationRepository medicationRepository,
        IMapper mapper,
        DomainEventDispatcher eventDispatcher)
    {
        _medicationRepository = medicationRepository;
        _mapper = mapper;
        _eventDispatcher = eventDispatcher;
        _unitOfWork = medicationRepository.UnitOfWork;
    }

    public async Task<MedicationDto> Handle(CreateMedicationCommand request,
        CancellationToken cancellationToken)
    {
        var medication = Medication.Create(
            request.Name,
            request.DosageForm,
            request.Strength);

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

        await _medicationRepository.AddAsync(medication, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<MedicationDto>(medication);
    }
}
