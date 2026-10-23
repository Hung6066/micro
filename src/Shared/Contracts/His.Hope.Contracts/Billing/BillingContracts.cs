namespace His.Hope.Contracts.Billing;

public sealed record BillingLineItemRequest(
    string Description,
    int Quantity,
    decimal UnitPrice,
    string? ItemCode,
    string? ItemTypeCode);

public sealed record CreateInvoiceRequest(
    Guid PatientId,
    Guid? EncounterId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime? DueDate,
    string? Notes,
    ICollection<BillingLineItemRequest> LineItems);

public sealed record AddInvoiceLineItemRequest(
    string Description,
    int Quantity,
    decimal UnitPrice,
    string? ItemCode,
    string? ItemTypeCode);

public sealed record RecordPaymentRequest(
    Guid PatientId,
    decimal Amount,
    DateTime PaymentDate,
    string MethodCode,
    string? ReferenceNumber,
    string? Notes);

public sealed record CancelInvoiceRequest(string Reason);

public sealed record VoidInvoiceRequest(string Reason);

public sealed record ApplyDiscountRequest(decimal Amount);

public sealed record ApplyTaxRequest(decimal Amount);
