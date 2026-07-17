using FluentAssertions;
using His.Hope.SharedKernel.Domain.ValueObjects;

namespace His.Hope.SharedKernel.Tests;

public class ContactInfoTests
{
    [Fact]
    public void Constructor_WithPhoneOnly_ShouldSetProperties()
    {
        var contact = new ContactInfo("+1234567890");
        contact.Phone.Should().Be("+1234567890");
        contact.Email.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithPhoneAndEmail_ShouldSetBoth()
    {
        var contact = new ContactInfo("+1234567890", "test@example.com");
        contact.Phone.Should().Be("+1234567890");
        contact.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_WithNullPhone_ShouldThrow()
    {
        var act = () => new ContactInfo(null!);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("phone");
    }

    [Fact]
    public void Constructor_WithEmptyPhone_ShouldThrow()
    {
        var act = () => new ContactInfo("");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("phone");
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var c1 = new ContactInfo("+123", "a@b.com");
        var c2 = new ContactInfo("+123", "a@b.com");
        c1.Should().Be(c2);
        c1.GetHashCode().Should().Be(c2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentPhones_ShouldNotBeEqual()
    {
        var c1 = new ContactInfo("+123");
        var c2 = new ContactInfo("+456");
        c1.Should().NotBe(c2);
    }

    [Fact]
    public void Equality_NullVsEmptyEmail_ShouldBeEqual()
    {
        var c1 = new ContactInfo("+123", null);
        var c2 = new ContactInfo("+123", "");
        c1.Should().Be(c2);
    }
}

public class AddressTests
{
    [Fact]
    public void Constructor_WithAllValues_ShouldSetProperties()
    {
        var address = new Address("123 Main St", "Downtown", "Metropolis", "State", "12345", "USA");
        address.Street.Should().Be("123 Main St");
        address.District.Should().Be("Downtown");
        address.City.Should().Be("Metropolis");
        address.Province.Should().Be("State");
        address.PostalCode.Should().Be("12345");
        address.Country.Should().Be("USA");
    }

    [Fact]
    public void Constructor_WithNullStreet_ShouldThrow()
    {
        var act = () => new Address(null!, "District", "City", "Province", "12345", "USA");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("street");
    }

    [Fact]
    public void Constructor_WithEmptyCity_ShouldThrow()
    {
        var act = () => new Address("Street", "District", "", "Province", "12345", "USA");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("city");
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a1 = new Address("123 St", "D", "C", "P", "1", "USA");
        var a2 = new Address("123 St", "D", "C", "P", "1", "USA");
        a1.Should().Be(a2);
        a1.GetHashCode().Should().Be(a2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentCountry_ShouldNotBeEqual()
    {
        var a1 = new Address("123 St", "D", "C", "P", "1", "USA");
        var a2 = new Address("123 St", "D", "C", "P", "1", "Canada");
        a1.Should().NotBe(a2);
    }

    [Fact]
    public void Equality_DifferentPostalCode_ShouldNotBeEqual()
    {
        var a1 = new Address("123 St", "D", "C", "P", "12345", "USA");
        var a2 = new Address("123 St", "D", "C", "P", "67890", "USA");
        a1.Should().NotBe(a2);
    }

    [Fact]
    public void Constructor_WithEmptyPostalCode_ShouldAllow()
    {
        var address = new Address("Street", "District", "City", "Province", "", "USA");
        address.PostalCode.Should().Be("");
    }
}
