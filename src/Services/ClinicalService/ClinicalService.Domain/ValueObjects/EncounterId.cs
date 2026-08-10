using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.ClinicalService.Domain.ValueObjects;

public class EncounterId : ValueObject
{
    public Guid Value { get; }
    public EncounterId(Guid value) => Value = value == Guid.Empty
        ? throw new ArgumentException("EncounterId cannot be empty", nameof(value)) : value;
    public static EncounterId New() => new(Guid.NewGuid());
    public static EncounterId From(Guid value) => new(value);
    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
    public override string ToString() => Value.ToString();
}
