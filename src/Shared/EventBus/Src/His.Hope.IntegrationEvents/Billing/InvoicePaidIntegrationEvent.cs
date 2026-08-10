using His.Hope.EventBus.Abstractions;

namespace His.Hope.IntegrationEvents.Billing;

public class InvoicePaidIntegrationEvent : IntegrationEvent
{
    public Guid InvoiceId { get; }
    public Guid PatientId { get; }
    public decimal AmountPaid { get; }
    public decimal TotalAmount { get; }

    public InvoicePaidIntegrationEvent(
        Guid invoiceId,
        Guid patientId,
        decimal amountPaid,
        decimal totalAmount)
    {
        InvoiceId = invoiceId;
        PatientId = patientId;
        AmountPaid = amountPaid;
        TotalAmount = totalAmount;
    }
}
