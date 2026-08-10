using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.Events;

public class InvoiceCreatedDomainEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public Guid PatientId { get; }
    public string InvoiceNumber { get; }
    public decimal TotalAmount { get; }

    public InvoiceCreatedDomainEvent(
        Guid invoiceId,
        Guid patientId,
        string invoiceNumber,
        decimal totalAmount)
    {
        InvoiceId = invoiceId;
        PatientId = patientId;
        InvoiceNumber = invoiceNumber;
        TotalAmount = totalAmount;
    }
}
