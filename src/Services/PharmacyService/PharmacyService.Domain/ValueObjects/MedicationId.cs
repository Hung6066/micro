using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.ValueObjects;

public class MedicationId : ValueObject
{
    public Guid Value { get; }

    public MedicationId(Guid value)
    {
        Value = value == Guid.Empty
            ? throw new ArgumentException("MedicationId cannot be empty", nameof(value))
            : value;
    }

    public static MedicationId New() => new(Guid.NewGuid());

    public static MedicationId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
