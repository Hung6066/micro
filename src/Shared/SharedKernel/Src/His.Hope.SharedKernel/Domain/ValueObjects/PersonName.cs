using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.SharedKernel.Domain.ValueObjects;

public class PersonName : ValueObject
{
    public string FirstName { get; }
    public string LastName { get; }
    public string? MiddleName { get; }

    public string FullName =>
        string.IsNullOrWhiteSpace(MiddleName)
            ? $"{LastName} {FirstName}"
            : $"{LastName} {MiddleName} {FirstName}";

    public static PersonName Create(string firstName, string lastName, string? middleName = null) =>
        new(firstName, lastName, middleName);

    public PersonName(string firstName, string lastName, string? middleName = null)
    {
        FirstName = Guard.Against.NullOrWhiteSpace(firstName, nameof(firstName));
        LastName = Guard.Against.NullOrWhiteSpace(lastName, nameof(lastName));
        MiddleName = middleName;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return FirstName;
        yield return LastName;
        yield return MiddleName ?? string.Empty;
    }
}
