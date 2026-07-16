using AutoMapper;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Application.DTOs;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Queries;

public class GetInvoiceByNumberQueryHandler : IRequestHandler<GetInvoiceByNumberQuery, InvoiceDto?>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMapper _mapper;

    public GetInvoiceByNumberQueryHandler(IInvoiceRepository invoiceRepository, IMapper mapper)
    {
        _invoiceRepository = invoiceRepository;
        _mapper = mapper;
    }

    public async Task<InvoiceDto?> Handle(GetInvoiceByNumberQuery request,
        CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByInvoiceNumberAsync(request.InvoiceNumber, cancellationToken);
        return invoice is null ? null : _mapper.Map<InvoiceDto>(invoice);
    }
}
