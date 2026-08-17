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
    DateTime? DateTo = null,
    IReadOnlySet<string>? FacilityIds = null,
    bool CrossFacility = false)
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
        var result = request.FacilityIds is null
            ? await _labOrderRepository.SearchAsync(request.Term, request.Page, request.PageSize,
                request.PatientId, request.Status, request.DateFrom, request.DateTo, cancellationToken)
            : await _labOrderRepository.SearchAsync(request.Term, request.Page, request.PageSize,
                request.PatientId, request.Status, request.DateFrom, request.DateTo,
                request.FacilityIds, request.CrossFacility, cancellationToken);
        var (items, totalCount) = result;

        var dtos = _mapper.Map<List<LabOrderDto>>(items);

        return new PagedResult<LabOrderDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
