using His.Hope.EventBus.Abstractions;

namespace His.Hope.IntegrationEvents.Billing;

public class InvoiceCreatedIntegrationEvent : IntegrationEvent
{
    public Guid InvoiceId { get; }
    public Guid PatientId { get; }
    public string InvoiceNumber { get; }
    public decimal TotalAmount { get; }

    public InvoiceCreatedIntegrationEvent(
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
