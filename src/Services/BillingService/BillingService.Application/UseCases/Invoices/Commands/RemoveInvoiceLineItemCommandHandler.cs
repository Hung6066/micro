using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.BillingService.Application.Common.Exceptions;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public class RemoveInvoiceLineItemCommandHandler : IRequestHandler<RemoveInvoiceLineItemCommand>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveInvoiceLineItemCommandHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
        _unitOfWork = invoiceRepository.UnitOfWork;
    }

    public async Task Handle(RemoveInvoiceLineItemCommand request,
        CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.From(request.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
            throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        var lineItemId = InvoiceLineItemId.From(request.LineItemId);
        invoice.RemoveLineItem(lineItemId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
