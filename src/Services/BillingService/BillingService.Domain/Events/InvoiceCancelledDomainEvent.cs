using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.Events;

public class InvoiceCancelledDomainEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public string Reason { get; }

    public InvoiceCancelledDomainEvent(Guid invoiceId, string reason)
    {
        InvoiceId = invoiceId;
        Reason = reason;
    }
}
