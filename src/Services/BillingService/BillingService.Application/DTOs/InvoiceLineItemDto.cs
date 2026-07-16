namespace His.Hope.BillingService.Application.DTOs;

public class InvoiceLineItemDto
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemTypeCode { get; set; }
    public string? ItemTypeName { get; set; }
    public DateTime CreatedAt { get; set; }
}
