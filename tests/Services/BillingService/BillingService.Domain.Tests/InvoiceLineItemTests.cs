using FluentAssertions;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.ValueObjects;
using Xunit;

namespace His.Hope.BillingService.Domain.Tests;

public class InvoiceLineItemTests
{
    private static readonly InvoiceId DefaultInvoiceId = InvoiceId.New();
    private const string DefaultDescription = "Consultation fee";
    private const int DefaultQuantity = 2;
    private const decimal DefaultUnitPrice = 75.00m;
    private const string DefaultItemCode = "CONS001";
    private static readonly InvoiceLineItemType DefaultItemType = InvoiceLineItemType.Service;

    private InvoiceLineItem CreateDefaultLineItem()
    {
        return InvoiceLineItem.Create(
            DefaultInvoiceId,
            DefaultDescription,
            DefaultQuantity,
            DefaultUnitPrice,
            DefaultItemCode,
            DefaultItemType);
    }

    [Fact]
    public void Create_WithValidParameters_CreatesLineItem()
    {
        var lineItem = InvoiceLineItem.Create(
            DefaultInvoiceId,
            DefaultDescription,
            DefaultQuantity,
            DefaultUnitPrice,
            DefaultItemCode,
            DefaultItemType);

        lineItem.Should().NotBeNull();
        lineItem.InvoiceId.Should().Be(DefaultInvoiceId);
        lineItem.Description.Should().Be(DefaultDescription);
        lineItem.Quantity.Should().Be(DefaultQuantity);
        lineItem.UnitPrice.Should().Be(DefaultUnitPrice);
        lineItem.ItemCode.Should().Be(DefaultItemCode);
        lineItem.ItemType.Should().Be(DefaultItemType);
        lineItem.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithZeroQuantity_Throws()
    {
        var act = () => InvoiceLineItem.Create(
            DefaultInvoiceId,
            DefaultDescription,
            0,
            DefaultUnitPrice,
            DefaultItemCode,
            DefaultItemType);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("quantity");
    }

    [Fact]
    public void Create_WithNegativeQuantity_Throws()
    {
        var act = () => InvoiceLineItem.Create(
            DefaultInvoiceId,
            DefaultDescription,
            -1,
            DefaultUnitPrice,
            DefaultItemCode,
            DefaultItemType);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("quantity");
    }

    [Fact]
    public void Create_WithZeroUnitPrice_Throws()
    {
        var act = () => InvoiceLineItem.Create(
            DefaultInvoiceId,
            DefaultDescription,
            DefaultQuantity,
            0,
            DefaultItemCode,
            DefaultItemType);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("unitPrice");
    }

    [Fact]
    public void Create_WithEmptyDescription_Throws()
    {
        var act = () => InvoiceLineItem.Create(
            DefaultInvoiceId,
            "",
            DefaultQuantity,
            DefaultUnitPrice,
            DefaultItemCode,
            DefaultItemType);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("description");
    }

    [Fact]
    public void Amount_EqualsQuantityTimesUnitPrice()
    {
        var lineItem = CreateDefaultLineItem();

        lineItem.Amount.Should().Be(DefaultQuantity * DefaultUnitPrice);
        lineItem.Amount.Should().Be(150.00m);
    }

    [Fact]
    public void Update_ModifiesQuantityAndPrice()
    {
        var lineItem = CreateDefaultLineItem();

        lineItem.Update(3, 100.00m);

        lineItem.Quantity.Should().Be(3);
        lineItem.UnitPrice.Should().Be(100.00m);
        lineItem.Amount.Should().Be(300.00m);
    }

    [Fact]
    public void Update_WithInvalidQuantity_Throws()
    {
        var lineItem = CreateDefaultLineItem();

        var act = () => lineItem.Update(0, 100.00m);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("quantity");
    }

    [Fact]
    public void Update_WithInvalidUnitPrice_Throws()
    {
        var lineItem = CreateDefaultLineItem();

        var act = () => lineItem.Update(3, 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("unitPrice");
    }

    [Fact]
    public void Create_WithNullItemType_CreatesLineItem()
    {
        var lineItem = InvoiceLineItem.Create(
            DefaultInvoiceId,
            DefaultDescription,
            DefaultQuantity,
            DefaultUnitPrice,
            null,
            null);

        lineItem.ItemCode.Should().BeNull();
        lineItem.ItemType.Should().BeNull();
    }

    [Fact]
    public void Create_WithDifferentItemTypes_SetsCorrectly()
    {
        var types = new[]
        {
            InvoiceLineItemType.Service,
            InvoiceLineItemType.Medication,
            InvoiceLineItemType.Supply,
            InvoiceLineItemType.Procedure,
            InvoiceLineItemType.Lab,
            InvoiceLineItemType.Consultation
        };

        foreach (var type in types)
        {
            var lineItem = InvoiceLineItem.Create(
                DefaultInvoiceId,
                DefaultDescription,
                1,
                100m,
                null,
                type);
            lineItem.ItemType.Should().Be(type);
        }
    }
}
