namespace His.Hope.BillingService.Application.DTOs;

public class InvoiceDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid? EncounterId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<InvoiceLineItemDto> LineItems { get; set; } = [];
    public List<PaymentDto> Payments { get; set; } = [];
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceDue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
