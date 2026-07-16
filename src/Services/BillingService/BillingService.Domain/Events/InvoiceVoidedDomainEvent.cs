using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.Events;

public class InvoiceVoidedDomainEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public string Reason { get; }

    public InvoiceVoidedDomainEvent(Guid invoiceId, string reason)
    {
        InvoiceId = invoiceId;
        Reason = reason;
    }
}
