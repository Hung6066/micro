using AutoMapper;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Queries;

public class GetPrescriptionsByPatientQueryHandler : IRequestHandler<GetPrescriptionsByPatientQuery, IReadOnlyList<PrescriptionDto>>
{
    private readonly IPrescriptionRepository _prescriptionRepository;
    private readonly IMapper _mapper;

    public GetPrescriptionsByPatientQueryHandler(IPrescriptionRepository prescriptionRepository, IMapper mapper)
    {
        _prescriptionRepository = prescriptionRepository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<PrescriptionDto>> Handle(GetPrescriptionsByPatientQuery request,
        CancellationToken cancellationToken)
    {
        var prescriptions = await _prescriptionRepository.GetByPatientIdAsync(request.PatientId, cancellationToken);
        return _mapper.Map<List<PrescriptionDto>>(prescriptions);
    }
}
