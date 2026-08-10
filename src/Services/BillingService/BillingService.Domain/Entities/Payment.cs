using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.BillingService.Domain.Entities;

public class Payment : Entity<PaymentId>
{
    public InvoiceId InvoiceId { get; private set; }
    public Guid PatientId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime PaymentDate { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Payment(
        PaymentId id,
        InvoiceId invoiceId,
        Guid patientId,
        decimal amount,
        DateTime paymentDate,
        PaymentMethod method,
        string? referenceNumber,
        string? notes)
        : base(id)
    {
        InvoiceId = invoiceId;
        PatientId = patientId;
        Amount = amount > 0 ? amount : throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");
        PaymentDate = paymentDate;
        Method = Guard.Against.Null(method, nameof(method));
        ReferenceNumber = referenceNumber;
        Status = PaymentStatus.Pending;
        Notes = notes;
        CreatedAt = DateTime.UtcNow;
    }

    public static Payment Create(
        InvoiceId invoiceId,
        Guid patientId,
        decimal amount,
        DateTime paymentDate,
        PaymentMethod method,
        string? referenceNumber,
        string? notes)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");
        Guard.Against.Null(method, nameof(method));

        var id = PaymentId.New();
        return new Payment(id, invoiceId, patientId, amount, paymentDate, method, referenceNumber, notes);
    }

    public void MarkCompleted()
    {
        Status = PaymentStatus.Completed;
    }

    public void MarkFailed()
    {
        Status = PaymentStatus.Failed;
    }

    public void Refund()
    {
        if (Status == PaymentStatus.Refunded)
            throw new DomainException("Payment has already been refunded.");

        if (Status != PaymentStatus.Completed)
            throw new DomainException("Only completed payments can be refunded.");

        Status = PaymentStatus.Refunded;
    }

    private Payment() { }
}
