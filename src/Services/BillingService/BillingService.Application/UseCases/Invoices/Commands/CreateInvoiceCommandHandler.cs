using AutoMapper;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.BillingService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, InvoiceDto>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public CreateInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        IMapper mapper)
    {
        _invoiceRepository = invoiceRepository;
        _mapper = mapper;
        _unitOfWork = invoiceRepository.UnitOfWork;
    }

    public async Task<InvoiceDto> Handle(CreateInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        var invoice = Invoice.Create(
            request.PatientId,
            request.EncounterId,
            request.InvoiceNumber,
            request.InvoiceDate,
            request.DueDate,
            request.Notes);

        foreach (var item in request.LineItems)
        {
            InvoiceLineItemType? itemType = null;
            if (!string.IsNullOrEmpty(item.ItemTypeCode))
                itemType = InvoiceLineItemType.FromCode(item.ItemTypeCode);

            var lineItem = InvoiceLineItem.Create(
                invoice.Id,
                item.Description,
                item.Quantity,
                item.UnitPrice,
                item.ItemCode,
                itemType);

            invoice.AddLineItem(lineItem);
        }

        await _invoiceRepository.AddAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<InvoiceDto>(invoice);
    }
}
