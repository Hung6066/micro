using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.BillingService.Application.Common.Exceptions;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public class ApplyDiscountCommandHandler : IRequestHandler<ApplyDiscountCommand>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyDiscountCommandHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
        _unitOfWork = invoiceRepository.UnitOfWork;
    }

    public async Task Handle(ApplyDiscountCommand request,
        CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.From(request.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
            throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        invoice.ApplyDiscount(request.Amount);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
