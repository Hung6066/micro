using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.AppointmentService.Domain.ValueObjects;

public class AppointmentId : ValueObject
{
    public Guid Value { get; }

    public AppointmentId(Guid value)
    {
        Value = value == Guid.Empty
            ? throw new ArgumentException("AppointmentId cannot be empty", nameof(value))
            : value;
    }

    public static AppointmentId New() => new(Guid.NewGuid());
    public static AppointmentId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
