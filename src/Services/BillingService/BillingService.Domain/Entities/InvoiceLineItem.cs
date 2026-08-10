using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.Entities;

public class InvoiceLineItem : Entity<InvoiceLineItemId>
{
    public InvoiceId InvoiceId { get; private set; }
    public string Description { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Amount => Quantity * UnitPrice;
    public string? ItemCode { get; private set; }
    public InvoiceLineItemType? ItemType { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private InvoiceLineItem(
        InvoiceLineItemId id,
        InvoiceId invoiceId,
        string description,
        int quantity,
        decimal unitPrice,
        string? itemCode,
        InvoiceLineItemType? itemType)
        : base(id)
    {
        InvoiceId = invoiceId;
        Description = Guard.Against.NullOrWhiteSpace(description, nameof(description));
        Quantity = quantity > 0 ? quantity : throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        UnitPrice = unitPrice > 0 ? unitPrice : throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be greater than zero.");
        ItemCode = itemCode;
        ItemType = itemType;
        CreatedAt = DateTime.UtcNow;
    }

    public static InvoiceLineItem Create(
        InvoiceId invoiceId,
        string description,
        int quantity,
        decimal unitPrice,
        string? itemCode,
        InvoiceLineItemType? itemType)
    {
        Guard.Against.NullOrWhiteSpace(description, nameof(description));

        var id = InvoiceLineItemId.New();
        return new InvoiceLineItem(id, invoiceId, description, quantity, unitPrice, itemCode, itemType);
    }

    public void Update(int quantity, decimal unitPrice)
    {
        Quantity = quantity > 0 ? quantity : throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        UnitPrice = unitPrice > 0 ? unitPrice : throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be greater than zero.");
    }

    private InvoiceLineItem() { }
}
