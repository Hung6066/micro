using His.Hope.BillingService.Application.Common.Exceptions;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public record VoidInvoiceCommand(Guid Id, string Reason) : IRequest;

public class VoidInvoiceCommandHandler : IRequestHandler<VoidInvoiceCommand>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public VoidInvoiceCommandHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
        _unitOfWork = invoiceRepository.UnitOfWork;
    }

    public async Task Handle(VoidInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.From(request.Id);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
            throw new NotFoundException(nameof(Invoice), request.Id);

        invoice.Void(request.Reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
