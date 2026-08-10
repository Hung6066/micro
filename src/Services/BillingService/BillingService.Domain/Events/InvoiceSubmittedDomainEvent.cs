using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.Events;

public class InvoiceSubmittedDomainEvent : DomainEvent
{
    public Guid InvoiceId { get; }

    public InvoiceSubmittedDomainEvent(Guid invoiceId)
    {
        InvoiceId = invoiceId;
    }
}
