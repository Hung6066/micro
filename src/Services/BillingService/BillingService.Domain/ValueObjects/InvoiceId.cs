using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.ValueObjects;

public class InvoiceId : ValueObject
{
    public Guid Value { get; }

    public InvoiceId(Guid value)
    {
        Value = value == Guid.Empty
            ? throw new ArgumentException("InvoiceId cannot be empty", nameof(value))
            : value;
    }

    public static InvoiceId New() => new(Guid.NewGuid());

    public static InvoiceId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
