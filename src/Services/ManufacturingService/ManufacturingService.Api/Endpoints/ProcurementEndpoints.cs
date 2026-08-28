using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Caching;
using StackExchange.Redis;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder;
using static ManufacturingEndpointHelpers;

internal static class ProcurementEndpoints
{
    public static RouteGroupBuilder MapProcurementEndpoints(this RouteGroupBuilder api)
    {
                
                api.MapGet("/suppliers", (string? tenantKey, bool? active, int? limit, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetSuppliers(scopedTenant, active, limit ?? 100));
                });

                api.MapPost("/suppliers", (CreateSupplierRequest request, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_supplier");
                    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
                    try { return Results.Created("/api/v1/manufacturing/suppliers", store.CreateSupplier(request)); }
                    catch (InvalidOperationException ex) when (ex.Message == "supplier_code_exists")
                    { return ManufacturingProblem(StatusCodes.Status409Conflict, ex.Message); }
                    catch (InvalidOperationException ex)
                    { return ManufacturingProblem(StatusCodes.Status400BadRequest, ex.Message); }
                });

                api.MapGet("/purchase-orders", (string? tenantKey, string? status, int? limit, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetPurchaseOrders(scopedTenant, status, limit ?? 100));
                });

                api.MapGet("/purchase-orders/{purchaseOrderId:guid}/status-history", (Guid purchaseOrderId, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetPurchaseOrderStatusHistory(tenantKey, purchaseOrderId));
                });

                api.MapGet("/supplier-rfqs", (string? status, int? limit, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); return Results.Ok(store.GetSupplierRfqs(tenantKey, status, limit ?? 200));
                });

                api.MapPost("/supplier-rfqs", (CreateSupplierRfqRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.TenantKey) || !TenantMatches(context, request.TenantKey)) return Results.Forbid(); var result = store.CreateSupplierRfq(request); return result.Error switch { "invalid_supplier_rfq" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), "supplier_rfq_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created("/api/v1/manufacturing/supplier-rfqs", result.Rfq) };
                });

                api.MapPost("/supplier-rfqs/{rfqId:guid}/quotations", (Guid rfqId, CreateSupplierQuotationRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey) || request.SupplierRfqId != rfqId) return Results.Forbid(); var result = store.CreateSupplierQuotation(tenantKey, request); return result.Error switch { "supplier_rfq_not_found" or "supplier_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "invalid_supplier_quotation" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), "supplier_quotation_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Created($"/api/v1/manufacturing/supplier-rfqs/{rfqId}/quotations", result.Quotation) };
                });

                api.MapGet("/supplier-rfqs/{rfqId:guid}/quotations", (Guid rfqId, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetSupplierQuotations(tenantKey, rfqId, 200));
                });

                api.MapPatch("/suppliers/{supplierId:guid}", (Guid supplierId, UpdateSupplierRequest request, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_supplier");
                    var result = store.UpdateSupplier(tenantKey, supplierId, request);
                    return result.Error switch
                    {
                        "supplier_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "tenant_mismatch" => Results.Forbid(),
                        "supplier_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ => Results.Ok(result.Supplier)
                    };
                });

                api.MapGet("/inbound-receipts", (Guid? purchaseOrderId, int? limit, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetInboundReceipts(tenantKey, purchaseOrderId, limit ?? 100));
                });

                api.MapPost("/purchase-orders", (CreatePurchaseOrderRequest request, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.OrderNumber) ||
                        string.IsNullOrWhiteSpace(request.Currency) || request.SupplierId == Guid.Empty || request.Lines is null || request.Lines.Count == 0 ||
                        request.Lines.Any(x => string.IsNullOrWhiteSpace(x.MaterialSku) || string.IsNullOrWhiteSpace(x.Uom) || x.OrderedQuantity <= 0 || x.UnitPrice < 0))
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_purchase_order");
                    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
                    var result = store.CreatePurchaseOrder(request);
                    return result.Error switch
                    {
                        "supplier_not_found" or "supplier_inactive" or "material_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "supplier_material_not_approved" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        "supplier_not_approved" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        "tenant_mismatch" => Results.Forbid(),
                        "purchase_order_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        "invalid_purchase_order_status" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        _ => Results.Created("/api/v1/manufacturing/purchase-orders", result.Order)
                    };
                });

                api.MapPut("/purchase-orders/{purchaseOrderId:guid}", (Guid purchaseOrderId, UpdatePurchaseOrderRequest request, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.UpdatePurchaseOrder(tenantKey, purchaseOrderId, request);
                    return result.Error switch
                    {
                        "purchase_order_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "tenant_mismatch" => Results.Forbid(),
                        "supplier_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "purchase_order_not_editable" or "invalid_purchase_order" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "purchase_order_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ => Results.Ok(result.Order)
                    };
                });

                api.MapPost("/purchase-orders/{purchaseOrderId:guid}/status", (Guid purchaseOrderId, UpdatePurchaseOrderStatusRequest request, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (string.IsNullOrWhiteSpace(request.Status)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_purchase_order_status");
                    var result = store.UpdatePurchaseOrderStatus(tenantKey, purchaseOrderId, request.Status);
                    return result.Error switch
                    {
                        "purchase_order_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "tenant_mismatch" => Results.Forbid(),
                        "invalid_purchase_order_transition" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        _ => Results.Ok(result.Order)
                    };
                });

                api.MapPost("/purchase-orders/{purchaseOrderId:guid}/receipts", (Guid purchaseOrderId, ReceiveInboundLotRequest request, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (request.PurchaseOrderId != purchaseOrderId || string.IsNullOrWhiteSpace(request.MaterialSku) || string.IsNullOrWhiteSpace(request.ReceiptNumber) ||
                        string.IsNullOrWhiteSpace(request.SupplierLotCode) || string.IsNullOrWhiteSpace(request.FacilityId) || request.PurchaseOrderLineId == Guid.Empty)
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_inbound_receipt");
                    var result = store.ReceiveInboundLot(tenantKey, request);
                    return result.Error switch
                    {
                        "purchase_order_not_found" or "purchase_order_line_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "tenant_mismatch" => Results.Forbid(),
                        "invalid_receipt_quantity" or "material_mismatch" or "purchase_order_not_receivable" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "receipt_number_exists" or "supplier_lot_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        "over_receipt" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        _ => Results.Created($"/api/v1/manufacturing/lots/{result.Receipt!.LotId}/inbound-receipt", result.Receipt)
                    };
                });

                api.MapPost("/suppliers/{supplierId:guid}/approval", (Guid supplierId, SupplierApprovalRequest request, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(request.Status)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_supplier_approval");
                    var actor = context.User.Identity?.Name ?? request.Actor ?? "system";
                    var result = store.UpdateSupplierApproval(tenantKey, supplierId, request, actor);
                    return result.Error switch
                    {
                        "supplier_not_found" or "material_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "supplier_material_not_approved" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        "tenant_mismatch" => Results.Forbid(),
                        "invalid_supplier_approval_status" or "invalid_supplier_approval_transition" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        _ => Results.Ok(result.Supplier)
                    };
                });

                api.MapGet("/suppliers/{supplierId:guid}/certificates", (Guid supplierId, int? limit, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    return string.IsNullOrWhiteSpace(tenantKey)
                        ? Results.Forbid()
                        : Results.Ok(store.GetSupplierCertificates(tenantKey, supplierId, limit ?? 50));
                });

                api.MapPost("/suppliers/{supplierId:guid}/certificates", (Guid supplierId, CreateSupplierCertificateRequest request, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var actor = context.User.Identity?.Name ?? "system";
                    var result = store.CreateSupplierCertificate(tenantKey, supplierId, request, actor);
                    return result.Error switch
                    {
                        "supplier_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "supplier_certificate_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        "invalid_supplier_certificate" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        _ => Results.Created($"/api/v1/manufacturing/suppliers/{supplierId}/certificates/{result.Certificate!.Id}", result.Certificate)
                    };
                });

                api.MapGet("/suppliers/{supplierId:guid}/material-approvals", (Guid supplierId, int? limit, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    return string.IsNullOrWhiteSpace(tenantKey) ? Results.Forbid() : Results.Ok(store.GetSupplierMaterialApprovals(tenantKey, supplierId, limit ?? 100));
                });

                api.MapPost("/suppliers/{supplierId:guid}/material-approvals", (Guid supplierId, CreateSupplierMaterialApprovalRequest request, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.CreateSupplierMaterialApproval(tenantKey, supplierId, request, context.User.Identity?.Name ?? "system");
                    return result.Error switch
                    {
                        "supplier_not_found" or "material_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "supplier_material_approval_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        "invalid_supplier_material_approval" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        _ => Results.Created($"/api/v1/manufacturing/suppliers/{supplierId}/material-approvals/{result.Approval!.Id}", result.Approval)
                    };
                });

                api.MapPost("/supplier-quotations/{quotationId:guid}/status", (Guid quotationId, UpdateSupplierQuotationStatusRequest request, HttpContext context, IManufacturingMasterDataStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateSupplierQuotationStatus(tenantKey, quotationId, request.Status);
                    return result.Error switch { "supplier_quotation_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "invalid_supplier_quotation_status" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), _ => Results.Ok(result.Quotation) };
                });

                api.MapPost("/purchase-orders/{purchaseOrderId:guid}/receipts/batch", (Guid purchaseOrderId, ReceiveInboundBatchRequest request, HttpContext context, IManufacturingProcurementStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.ReceiveInboundBatch(tenantKey, purchaseOrderId, request);
                    return result.Error switch
                    {
                        "invalid_inbound_batch" or "invalid_inbound_receipt" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "purchase_order_not_found" or "purchase_order_line_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "tenant_mismatch" => Results.Forbid(),
                        "receipt_number_exists" or "supplier_lot_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        "over_receipt" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        _ => Results.Ok(result.Receipts)
                    };
                });

        return api;
    }
}





