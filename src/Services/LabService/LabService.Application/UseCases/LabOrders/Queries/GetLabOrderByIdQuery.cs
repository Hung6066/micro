using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.Entities;
using AutoMapper;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Queries;

public record GetLabOrderByIdQuery(Guid Id) : IRequest<LabOrderDto?>;

public class GetLabOrderByIdQueryHandler : IRequestHandler<GetLabOrderByIdQuery, LabOrderDto?>
{
    private readonly ILabOrderRepository _labOrderRepository;
    private readonly IMapper _mapper;

    public GetLabOrderByIdQueryHandler(ILabOrderRepository labOrderRepository, IMapper mapper)
    {
        _labOrderRepository = labOrderRepository;
        _mapper = mapper;
    }

    public async Task<LabOrderDto?> Handle(GetLabOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var labOrderId = LabOrderId.From(request.Id);
        var labOrder = await _labOrderRepository.GetByIdAsync(labOrderId, cancellationToken);

        return labOrder is null ? null : _mapper.Map<LabOrderDto>(labOrder);
    }
}
