using AutoMapper;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Application.DTOs;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Queries;

public class SearchInvoicesQueryHandler : IRequestHandler<SearchInvoicesQuery, PagedResult<InvoiceDto>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMapper _mapper;

    public SearchInvoicesQueryHandler(IInvoiceRepository invoiceRepository, IMapper mapper)
    {
        _invoiceRepository = invoiceRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<InvoiceDto>> Handle(SearchInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _invoiceRepository.SearchAsync(
            request.Term, request.Page, request.PageSize,
            request.PatientId, request.Status, request.DateFrom, request.DateTo,
            cancellationToken);

        var dtos = _mapper.Map<List<InvoiceDto>>(result.Items);

        return new PagedResult<InvoiceDto>(dtos, result.TotalCount, request.Page, request.PageSize);
    }
}
