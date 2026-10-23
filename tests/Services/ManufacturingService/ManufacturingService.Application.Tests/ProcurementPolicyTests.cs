using His.Hope.ManufacturingService.Application;

namespace ManufacturingService.Application.Tests;

public sealed class ProcurementPolicyTests
{
    [Fact]
    public void AcceptsApprovedInboundWithinOrderQuantity()
    {
        var input = new InboundReceiptValidationInput(5m, "nacoms", "nacoms", "Approved", "MANGO", "mango", 5m, 20m);

        Assert.Null(ProcurementPolicy.ValidateInboundReceipt(input));
    }

    [Theory]
    [InlineData("purchase_order_not_receivable")]
    [InlineData("material_mismatch")]
    [InlineData("over_receipt")]
    [InlineData("tenant_mismatch")]
    public void RejectsInvalidInboundReceipt(string expected)
    {
        var input = new InboundReceiptValidationInput(
            expected == "over_receipt" ? 16m : 5m,
            "nacoms",
            expected == "tenant_mismatch" ? "other" : "nacoms",
            expected == "purchase_order_not_receivable" ? "Draft" : "Approved",
            "MANGO",
            expected == "material_mismatch" ? "PINEAPPLE" : "MANGO",
            5m,
            20m);

        Assert.Equal(expected, ProcurementPolicy.ValidateInboundReceipt(input));
    }

    [Fact]
    public void RejectsEmptyPurchaseOrder()
    {
        var input = new PurchaseOrderValidationInput("Draft", "nacoms", "nacoms", true, " ", 0);

        Assert.Equal("invalid_purchase_order", ProcurementPolicy.ValidatePurchaseOrder(input));
    }
}
