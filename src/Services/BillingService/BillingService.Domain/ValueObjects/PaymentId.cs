using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.ValueObjects;

public class PaymentId : ValueObject
{
    public Guid Value { get; }

    public PaymentId(Guid value)
    {
        Value = value == Guid.Empty
            ? throw new ArgumentException("PaymentId cannot be empty", nameof(value))
            : value;
    }

    public static PaymentId New() => new(Guid.NewGuid());

    public static PaymentId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
