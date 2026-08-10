using AutoMapper;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.BillingService.Application.Common.Exceptions;
using His.Hope.BillingService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public class AddInvoiceLineItemCommandHandler : IRequestHandler<AddInvoiceLineItemCommand, InvoiceDto>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public AddInvoiceLineItemCommandHandler(
        IInvoiceRepository invoiceRepository,
        IMapper mapper)
    {
        _invoiceRepository = invoiceRepository;
        _mapper = mapper;
        _unitOfWork = invoiceRepository.UnitOfWork;
    }

    public async Task<InvoiceDto> Handle(AddInvoiceLineItemCommand request,
        CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.From(request.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
            throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        InvoiceLineItemType? itemType = null;
        if (!string.IsNullOrEmpty(request.ItemTypeCode))
            itemType = InvoiceLineItemType.FromCode(request.ItemTypeCode);

        var lineItem = InvoiceLineItem.Create(
            invoiceId,
            request.Description,
            request.Quantity,
            request.UnitPrice,
            request.ItemCode,
            itemType);

        invoice.AddLineItem(lineItem);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<InvoiceDto>(invoice);
    }
}
