using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.ValueObjects;

public class InvoiceStatus : Enumeration<InvoiceStatus>
{
    public static readonly InvoiceStatus Draft = new("DRAFT", "Draft");
    public static readonly InvoiceStatus Submitted = new("SUBMITTED", "Submitted");
    public static readonly InvoiceStatus PartiallyPaid = new("PARTIALLY_PAID", "Partially Paid");
    public static readonly InvoiceStatus Paid = new("PAID", "Paid");
    public static readonly InvoiceStatus Cancelled = new("CANCELLED", "Cancelled");
    public static readonly InvoiceStatus Overdue = new("OVERDUE", "Overdue");
    public static readonly InvoiceStatus Voided = new("VOIDED", "Voided");

    private InvoiceStatus(string code, string name) : base(code, name) { }
}
