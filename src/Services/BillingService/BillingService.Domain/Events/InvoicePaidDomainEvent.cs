using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.Events;

public class InvoicePaidDomainEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public Guid PatientId { get; }
    public decimal AmountPaid { get; }
    public decimal TotalAmount { get; }

    public InvoicePaidDomainEvent(Guid invoiceId, Guid patientId, decimal amountPaid, decimal totalAmount)
    {
        InvoiceId = invoiceId;
        PatientId = patientId;
        AmountPaid = amountPaid;
        TotalAmount = totalAmount;
    }
}
