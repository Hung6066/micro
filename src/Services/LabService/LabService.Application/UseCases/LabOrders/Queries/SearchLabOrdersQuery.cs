using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Queries;

public record SearchLabOrdersQuery(
    string Term = "",
    int Page = 1,
    int PageSize = 20,
    Guid? PatientId = null,
    string? Status = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null)
    : IRequest<PagedResult<LabOrderDto>>;

public class SearchLabOrdersQueryHandler : IRequestHandler<SearchLabOrdersQuery, PagedResult<LabOrderDto>>
{
    private readonly ILabOrderRepository _labOrderRepository;
    private readonly IMapper _mapper;

    public SearchLabOrdersQueryHandler(ILabOrderRepository labOrderRepository, IMapper mapper)
    {
        _labOrderRepository = labOrderRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<LabOrderDto>> Handle(SearchLabOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _labOrderRepository.SearchAsync(
            request.Term, request.Page, request.PageSize,
            request.PatientId, request.Status, request.DateFrom, request.DateTo,
            cancellationToken);

        var dtos = _mapper.Map<List<LabOrderDto>>(items);

        return new PagedResult<LabOrderDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
