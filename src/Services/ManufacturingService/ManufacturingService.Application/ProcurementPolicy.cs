using His.Hope.ManufacturingService.Domain;

namespace His.Hope.ManufacturingService.Application;

public sealed record PurchaseOrderValidationInput(
    string Status,
    string TenantKey,
    string SupplierTenantKey,
    bool SupplierActive,
    string OrderNumber,
    int LineCount);

public sealed record InboundReceiptValidationInput(
    decimal Quantity,
    string TenantKey,
    string OrderTenantKey,
    string OrderStatus,
    string LineMaterialSku,
    string RequestMaterialSku,
    decimal ReceivedQuantity,
    decimal OrderedQuantity);

public static class ProcurementPolicy
{
    private static readonly string[] AllowedOrderStatuses = [ManufacturingStatusCodes.Draft, ManufacturingStatusCodes.Approved, ManufacturingStatusCodes.PartiallyReceived, ManufacturingStatusCodes.Closed, ManufacturingStatusCodes.Cancelled];

    public static string? ValidatePurchaseOrder(PurchaseOrderValidationInput input)
    {
        if (!AllowedOrderStatuses.Contains(input.Status, StringComparer.OrdinalIgnoreCase)) return "invalid_purchase_order_status";
        if (string.IsNullOrWhiteSpace(input.OrderNumber) || input.LineCount == 0) return "invalid_purchase_order";
        if (!input.SupplierActive) return ManufacturingErrorCodes.SupplierInactive;
        if (!input.SupplierTenantKey.Equals(input.TenantKey, StringComparison.OrdinalIgnoreCase)) return ManufacturingErrorCodes.TenantMismatch;
        return null;
    }

    public static string? ValidateInboundReceipt(InboundReceiptValidationInput input)
    {
        if (input.Quantity <= 0) return "invalid_receipt_quantity";
        if (!input.TenantKey.Equals(input.OrderTenantKey, StringComparison.OrdinalIgnoreCase)) return ManufacturingErrorCodes.TenantMismatch;
        if (input.OrderStatus is not (ManufacturingStatusCodes.Approved or ManufacturingStatusCodes.PartiallyReceived)) return "purchase_order_not_receivable";
        if (!input.LineMaterialSku.Equals(input.RequestMaterialSku, StringComparison.OrdinalIgnoreCase)) return ManufacturingErrorCodes.MaterialMismatch;
        if (input.ReceivedQuantity + input.Quantity > input.OrderedQuantity) return "over_receipt";
        return null;
    }
}
