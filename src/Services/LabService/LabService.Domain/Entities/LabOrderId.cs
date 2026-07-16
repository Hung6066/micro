using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Entities;

public class LabOrderId : ValueObject
{
    public Guid Value { get; }

    public LabOrderId(Guid value)
    {
        Value = value == Guid.Empty
            ? throw new ArgumentException("LabOrderId cannot be empty", nameof(value))
            : value;
    }

    public static LabOrderId New() => new(Guid.NewGuid());

    public static LabOrderId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
