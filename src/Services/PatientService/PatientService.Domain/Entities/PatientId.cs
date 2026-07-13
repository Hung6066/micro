using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.Entities;

public class PatientId : ValueObject
{
    public Guid Value { get; }

    public PatientId(Guid value)
    {
        Value = value == Guid.Empty
            ? throw new ArgumentException("PatientId cannot be empty", nameof(value))
            : value;
    }

    public static PatientId New() => new(Guid.NewGuid());

    public static PatientId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
