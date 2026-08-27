using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingProcurementStore
{
    SupplierDto CreateSupplier(CreateSupplierRequest request);
    IReadOnlyList<SupplierDto> GetSuppliers(string tenantKey, bool? active, int limit);
    (SupplierDto? Supplier, string? Error) UpdateSupplier(string tenantKey, Guid supplierId, UpdateSupplierRequest request);
    (SupplierDto? Supplier, string? Error) UpdateSupplierApproval(string tenantKey, Guid supplierId, SupplierApprovalRequest request, string actor);
    (SupplierCertificateDto? Certificate, string? Error) CreateSupplierCertificate(string tenantKey, Guid supplierId, CreateSupplierCertificateRequest request, string actor);
    IReadOnlyList<SupplierCertificateDto> GetSupplierCertificates(string tenantKey, Guid supplierId, int limit);
    (SupplierMaterialApprovalDto? Approval, string? Error) CreateSupplierMaterialApproval(string tenantKey, Guid supplierId, CreateSupplierMaterialApprovalRequest request, string actor);
    IReadOnlyList<SupplierMaterialApprovalDto> GetSupplierMaterialApprovals(string tenantKey, Guid supplierId, int limit);
    (PurchaseOrderDto? Order, string? Error) CreatePurchaseOrder(CreatePurchaseOrderRequest request);
    IReadOnlyList<PurchaseOrderDto> GetPurchaseOrders(string tenantKey, string? status, int limit);
    (PurchaseOrderDto? Order, string? Error) UpdatePurchaseOrder(string tenantKey, Guid purchaseOrderId, UpdatePurchaseOrderRequest request);
    (PurchaseOrderDto? Order, string? Error) UpdatePurchaseOrderStatus(string tenantKey, Guid purchaseOrderId, string status);
    (InboundReceiptDto? Receipt, string? Error) ReceiveInboundLot(string tenantKey, ReceiveInboundLotRequest request);
    IReadOnlyList<InboundReceiptDto> GetInboundReceipts(string tenantKey, Guid? purchaseOrderId, int limit);
    (IReadOnlyList<InboundReceiptDto> Receipts, string? Error) ReceiveInboundBatch(string tenantKey, Guid purchaseOrderId, ReceiveInboundBatchRequest request);
}
