using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.Events;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.BillingService.Domain.Aggregates;

public class Invoice : AggregateRoot<InvoiceId>
{
    public Guid PatientId { get; private set; }
    public Guid? EncounterId { get; private set; }
    public string InvoiceNumber { get; private set; }
    public DateTime InvoiceDate { get; private set; }
    public DateTime? DueDate { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<InvoiceLineItem> _lineItems = [];
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    private readonly List<Payment> _payments = [];
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    public decimal SubTotal { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TotalAmount => SubTotal + TaxAmount - DiscountAmount;
    public decimal PaidAmount { get; private set; }
    public decimal BalanceDue => TotalAmount - PaidAmount;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Invoice(
        InvoiceId id,
        Guid patientId,
        Guid? encounterId,
        string invoiceNumber,
        DateTime invoiceDate,
        DateTime? dueDate,
        string? notes)
        : base(id)
    {
        PatientId = patientId;
        EncounterId = encounterId;
        InvoiceNumber = Guard.Against.NullOrWhiteSpace(invoiceNumber, nameof(invoiceNumber));
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        Notes = notes;
        Status = InvoiceStatus.Draft;
        SubTotal = 0;
        TaxAmount = 0;
        DiscountAmount = 0;
        PaidAmount = 0;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new InvoiceCreatedDomainEvent(
            Id.Value, patientId, invoiceNumber, TotalAmount));
    }

    public static Invoice Create(
        Guid patientId,
        Guid? encounterId,
        string invoiceNumber,
        DateTime invoiceDate,
        DateTime? dueDate,
        string? notes)
    {
        Guard.Against.NullOrWhiteSpace(invoiceNumber, nameof(invoiceNumber));

        var id = InvoiceId.New();
        return new Invoice(id, patientId, encounterId, invoiceNumber, invoiceDate, dueDate, notes);
    }

    public void AddLineItem(InvoiceLineItem lineItem)
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Cannot add line items to a non-draft invoice.");

        _lineItems.Add(lineItem);
        RecalculateSubTotal();
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveLineItem(InvoiceLineItemId lineItemId)
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Cannot remove line items from a non-draft invoice.");

        var lineItem = _lineItems.FirstOrDefault(li => li.Id == lineItemId);
        if (lineItem is null)
            throw new DomainException("Line item not found on this invoice.");

        _lineItems.Remove(lineItem);
        RecalculateSubTotal();
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyDiscount(decimal amount)
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Cannot apply discount to a non-draft invoice.");

        if (amount < 0)
            throw new DomainException("Discount amount cannot be negative.");

        if (amount > SubTotal)
            throw new DomainException("Discount cannot exceed subtotal.");

        DiscountAmount = amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyTax(decimal amount)
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Cannot apply tax to a non-draft invoice.");

        if (amount < 0)
            throw new DomainException("Tax amount cannot be negative.");

        TaxAmount = amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Submit()
    {
        if (Status == InvoiceStatus.Submitted)
            throw new DomainException("Invoice has already been submitted.");

        if (Status == InvoiceStatus.Paid)
            throw new DomainException("Cannot submit a paid invoice.");

        if (Status == InvoiceStatus.Cancelled)
            throw new DomainException("Cannot submit a cancelled invoice.");

        if (_lineItems.Count == 0)
            throw new DomainException("Cannot submit an invoice with no line items.");

        Status = InvoiceStatus.Submitted;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new InvoiceSubmittedDomainEvent(Id.Value));
    }

    public void MarkPaid()
    {
        if (Status == InvoiceStatus.Paid)
            throw new DomainException("Invoice is already paid.");

        if (Status == InvoiceStatus.Cancelled)
            throw new DomainException("Cannot mark a cancelled invoice as paid.");

        Status = InvoiceStatus.Paid;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new InvoicePaidDomainEvent(Id.Value, PaidAmount, TotalAmount));
    }

    public void Cancel(string reason)
    {
        if (Status == InvoiceStatus.Cancelled)
            throw new DomainException("Invoice has already been cancelled.");

        if (Status == InvoiceStatus.Paid)
            throw new DomainException("Cannot cancel a paid invoice.");

        Status = InvoiceStatus.Cancelled;
        Notes = Guard.Against.NullOrWhiteSpace(reason, nameof(reason));
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new InvoiceCancelledDomainEvent(Id.Value, reason));
    }

    public void Void(string reason)
    {
        if (Status == InvoiceStatus.Voided)
            throw new DomainException("Invoice has already been voided.");

        if (Status == InvoiceStatus.Paid)
            throw new DomainException("Cannot void a paid invoice.");

        Status = InvoiceStatus.Voided;
        Notes = Guard.Against.NullOrWhiteSpace(reason, nameof(reason));
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new InvoiceVoidedDomainEvent(Id.Value, reason));
    }

    public void RecordPayment(Payment payment)
    {
        if (Status == InvoiceStatus.Cancelled)
            throw new DomainException("Cannot record payment on a cancelled invoice.");

        if (Status == InvoiceStatus.Paid)
            throw new DomainException("Invoice is already fully paid.");

        _payments.Add(payment);

        if (payment.Status == PaymentStatus.Completed)
            PaidAmount += payment.Amount;

        if (BalanceDue <= 0)
        {
            Status = InvoiceStatus.Paid;
            AddDomainEvent(new InvoicePaidDomainEvent(Id.Value, payment.Amount, TotalAmount));
        }
        else if (PaidAmount > 0)
        {
            Status = InvoiceStatus.PartiallyPaid;
        }

        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentRecordedDomainEvent(payment.Id.Value, Id.Value, payment.Amount));
    }

    private void RecalculateSubTotal()
    {
        SubTotal = _lineItems.Sum(li => li.Amount);
    }

    private Invoice() { }
}
