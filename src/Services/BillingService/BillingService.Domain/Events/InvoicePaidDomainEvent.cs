using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.Events;

public class InvoicePaidDomainEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public decimal AmountPaid { get; }
    public decimal TotalAmount { get; }

    public InvoicePaidDomainEvent(Guid invoiceId, decimal amountPaid, decimal totalAmount)
    {
        InvoiceId = invoiceId;
        AmountPaid = amountPaid;
        TotalAmount = totalAmount;
    }
}
