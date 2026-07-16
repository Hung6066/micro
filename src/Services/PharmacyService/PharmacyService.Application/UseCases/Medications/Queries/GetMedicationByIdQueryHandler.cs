using AutoMapper;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Medications.Queries;

public class GetMedicationByIdQueryHandler : IRequestHandler<GetMedicationByIdQuery, MedicationDto?>
{
    private readonly IMedicationRepository _medicationRepository;
    private readonly IMapper _mapper;

    public GetMedicationByIdQueryHandler(IMedicationRepository medicationRepository, IMapper mapper)
    {
        _medicationRepository = medicationRepository;
        _mapper = mapper;
    }

    public async Task<MedicationDto?> Handle(GetMedicationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var medicationId = MedicationId.From(request.Id);
        var medication = await _medicationRepository.GetByIdAsync(medicationId, cancellationToken);

        return medication is null ? null : _mapper.Map<MedicationDto>(medication);
    }
}
