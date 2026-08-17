using AutoMapper;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Application.DTOs;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Queries;

public class GetInvoicesByPatientQueryHandler : IRequestHandler<GetInvoicesByPatientQuery, IReadOnlyList<InvoiceDto>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMapper _mapper;

    public GetInvoicesByPatientQueryHandler(IInvoiceRepository invoiceRepository, IMapper mapper)
    {
        _invoiceRepository = invoiceRepository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<InvoiceDto>> Handle(GetInvoicesByPatientQuery request,
        CancellationToken cancellationToken)
    {
        var invoices = request.FacilityIds is null
            ? await _invoiceRepository.GetByPatientAsync(request.PatientId, cancellationToken)
            : await _invoiceRepository.GetByPatientAsync(request.PatientId,
                request.FacilityIds, request.CrossFacility, cancellationToken);
        return _mapper.Map<List<InvoiceDto>>(invoices);
    }
}
