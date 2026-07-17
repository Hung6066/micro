using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.SharedKernel.Domain.ValueObjects;

public class Address : ValueObject
{
    public string Street { get; }
    public string District { get; }
    public string City { get; }
    public string Province { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public static Address Create(
        string street,
        string district,
        string city,
        string province,
        string postalCode,
        string country) =>
        new(street, district, city, province, postalCode, country);

    public Address(
        string street,
        string district,
        string city,
        string province,
        string postalCode,
        string country)
    {
        Street = Guard.Against.NullOrWhiteSpace(street, nameof(street));
        District = Guard.Against.NullOrWhiteSpace(district, nameof(district));
        City = Guard.Against.NullOrWhiteSpace(city, nameof(city));
        Province = Guard.Against.NullOrWhiteSpace(province, nameof(province));
        PostalCode = postalCode;
        Country = Guard.Against.NullOrWhiteSpace(country, nameof(country));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Street;
        yield return District;
        yield return City;
        yield return Province;
        yield return PostalCode;
        yield return Country;
    }
}
