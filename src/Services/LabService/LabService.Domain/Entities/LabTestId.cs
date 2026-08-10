using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Entities;

public class LabTestId : ValueObject
{
    public Guid Value { get; }

    public LabTestId(Guid value)
    {
        Value = value == Guid.Empty
            ? throw new ArgumentException("LabTestId cannot be empty", nameof(value))
            : value;
    }

    public static LabTestId New() => new(Guid.NewGuid());

    public static LabTestId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
