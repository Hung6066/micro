using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.ValueObjects;

public class InvoiceLineItemId : ValueObject
{
    public Guid Value { get; }

    public InvoiceLineItemId(Guid value)
    {
        Value = value == Guid.Empty
            ? throw new ArgumentException("InvoiceLineItemId cannot be empty", nameof(value))
            : value;
    }

    public static InvoiceLineItemId New() => new(Guid.NewGuid());

    public static InvoiceLineItemId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
