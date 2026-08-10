using AutoMapper;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.BillingService.Application.DTOs;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Queries;

public class GetInvoiceByIdQueryHandler : IRequestHandler<GetInvoiceByIdQuery, InvoiceDto?>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMapper _mapper;

    public GetInvoiceByIdQueryHandler(IInvoiceRepository invoiceRepository, IMapper mapper)
    {
        _invoiceRepository = invoiceRepository;
        _mapper = mapper;
    }

    public async Task<InvoiceDto?> Handle(GetInvoiceByIdQuery request,
        CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.From(request.Id);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);

        return invoice is null ? null : _mapper.Map<InvoiceDto>(invoice);
    }
}
