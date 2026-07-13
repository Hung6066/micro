using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.SharedKernel.Domain.ValueObjects;

public class ContactInfo : ValueObject
{
    public string Phone { get; }
    public string? Email { get; }

    public ContactInfo(string phone, string? email = null)
    {
        Phone = Guard.Against.NullOrWhiteSpace(phone, nameof(phone));
        Email = email;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Phone;
        yield return Email ?? string.Empty;
    }
}
