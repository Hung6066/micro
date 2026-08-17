using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Queries;

public record GetLabOrdersByPatientQuery(
    Guid PatientId, IReadOnlySet<string>? FacilityIds = null, bool CrossFacility = false)
    : IRequest<IReadOnlyList<LabOrderDto>>;

public class GetLabOrdersByPatientQueryHandler : IRequestHandler<GetLabOrdersByPatientQuery, IReadOnlyList<LabOrderDto>>
{
    private readonly ILabOrderRepository _labOrderRepository;
    private readonly IMapper _mapper;

    public GetLabOrdersByPatientQueryHandler(ILabOrderRepository labOrderRepository, IMapper mapper)
    {
        _labOrderRepository = labOrderRepository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<LabOrderDto>> Handle(GetLabOrdersByPatientQuery request,
        CancellationToken cancellationToken)
    {
        var labOrders = request.FacilityIds is null
            ? await _labOrderRepository.GetByPatientAsync(request.PatientId, cancellationToken)
            : await _labOrderRepository.GetByPatientAsync(request.PatientId,
                request.FacilityIds, request.CrossFacility, cancellationToken);
        return _mapper.Map<List<LabOrderDto>>(labOrders);
    }
}
