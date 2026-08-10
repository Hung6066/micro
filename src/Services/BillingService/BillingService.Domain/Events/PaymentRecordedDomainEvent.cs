using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.Events;

public class PaymentRecordedDomainEvent : DomainEvent
{
    public Guid PaymentId { get; }
    public Guid InvoiceId { get; }
    public decimal Amount { get; }

    public PaymentRecordedDomainEvent(Guid paymentId, Guid invoiceId, decimal amount)
    {
        PaymentId = paymentId;
        InvoiceId = invoiceId;
        Amount = amount;
    }
}
