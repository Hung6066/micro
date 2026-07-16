using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.ValueObjects;

public class InvoiceLineItemType : Enumeration<InvoiceLineItemType>
{
    public static readonly InvoiceLineItemType Service = new("SERVICE", "Service");
    public static readonly InvoiceLineItemType Medication = new("MEDICATION", "Medication");
    public static readonly InvoiceLineItemType Supply = new("SUPPLY", "Supply");
    public static readonly InvoiceLineItemType Procedure = new("PROCEDURE", "Procedure");
    public static readonly InvoiceLineItemType Lab = new("LAB", "Lab");
    public static readonly InvoiceLineItemType Consultation = new("CONSULTATION", "Consultation");

    private InvoiceLineItemType(string code, string name) : base(code, name) { }
}
