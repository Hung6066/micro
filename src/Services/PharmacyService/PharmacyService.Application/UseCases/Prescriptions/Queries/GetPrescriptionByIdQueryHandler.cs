using AutoMapper;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Queries;

public class GetPrescriptionByIdQueryHandler : IRequestHandler<GetPrescriptionByIdQuery, PrescriptionDto?>
{
    private readonly IPrescriptionRepository _prescriptionRepository;
    private readonly IMapper _mapper;

    public GetPrescriptionByIdQueryHandler(IPrescriptionRepository prescriptionRepository, IMapper mapper)
    {
        _prescriptionRepository = prescriptionRepository;
        _mapper = mapper;
    }

    public async Task<PrescriptionDto?> Handle(GetPrescriptionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var prescriptionId = PrescriptionId.From(request.Id);
        var prescription = await _prescriptionRepository.GetByIdAsync(prescriptionId, cancellationToken);

        return prescription is null ? null : _mapper.Map<PrescriptionDto>(prescription);
    }
}
