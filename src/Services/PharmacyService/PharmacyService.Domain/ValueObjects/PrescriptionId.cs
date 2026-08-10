using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.ValueObjects;

public class PrescriptionId : ValueObject
{
    public Guid Value { get; }

    public PrescriptionId(Guid value)
    {
        Value = value == Guid.Empty
            ? throw new ArgumentException("PrescriptionId cannot be empty", nameof(value))
            : value;
    }

    public static PrescriptionId New() => new(Guid.NewGuid());

    public static PrescriptionId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
