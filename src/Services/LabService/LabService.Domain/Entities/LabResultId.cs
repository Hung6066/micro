using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Entities;

public class LabResultId : ValueObject
{
    public Guid Value { get; }

    public LabResultId(Guid value)
    {
        Value = value == Guid.Empty
            ? throw new ArgumentException("LabResultId cannot be empty", nameof(value))
            : value;
    }

    public static LabResultId New() => new(Guid.NewGuid());

    public static LabResultId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
