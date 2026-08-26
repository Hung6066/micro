using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;

public sealed class ManufacturingProcurementStore(IDbContextFactory<ManufacturingDbContext> dbFactory) : IManufacturingProcurementStore
{
    public SupplierDto CreateSupplier(CreateSupplierRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        if (db.Suppliers.Any(x => x.TenantKey == request.TenantKey && x.Code == request.Code))
            throw new InvalidOperationException("supplier_code_exists");
        var entity = new ManufacturingSupplierEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), Code = request.Code.Trim(),
            Name = request.Name.Trim(), Active = request.Active, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Suppliers.Add(entity);
        db.SaveChanges();
        return ToDto(entity);
    }

    public IReadOnlyList<SupplierDto> GetSuppliers(string tenantKey, bool? active, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Suppliers.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        return query.OrderBy(x => x.Code).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    public (SupplierDto? Supplier, string? Error) UpdateSupplier(string tenantKey, Guid supplierId, UpdateSupplierRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var supplier = db.Suppliers.SingleOrDefault(x => x.Id == supplierId);
        if (supplier is null) return (null, "supplier_not_found");
        if (!supplier.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
        if (db.Suppliers.Any(x => x.Id != supplierId && x.TenantKey == tenantKey && x.Code == request.Code.Trim())) return (null, "supplier_code_exists");
        supplier.Code = request.Code.Trim();
        supplier.Name = request.Name.Trim();
        supplier.Active = request.Active;
        db.SaveChanges();
        return (ToDto(supplier), null);
    }

    public (PurchaseOrderDto? Order, string? Error) CreatePurchaseOrder(CreatePurchaseOrderRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var supplier = db.Suppliers.SingleOrDefault(x => x.Id == request.SupplierId);
        if (supplier is null) return (null, "supplier_not_found");
        var policyError = ProcurementPolicy.ValidatePurchaseOrder(new PurchaseOrderValidationInput(
            request.Status, request.TenantKey, supplier.TenantKey, supplier.Active, request.OrderNumber, request.Lines.Count));
        if (policyError is not null) return (null, policyError);
        if (db.PurchaseOrders.Any(x => x.TenantKey == request.TenantKey && x.OrderNumber == request.OrderNumber.Trim())) return (null, "purchase_order_exists");

        var entity = new ManufacturingPurchaseOrderEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), SupplierId = supplier.Id,
            OrderNumber = request.OrderNumber.Trim(), Status = request.Status.Trim(), Currency = request.Currency.Trim(),
            OrderedAt = request.OrderedAt ?? DateTimeOffset.UtcNow, ExpectedAt = request.ExpectedAt,
            Lines = request.Lines.Select(x => new ManufacturingPurchaseOrderLineEntity
            {
                Id = Guid.NewGuid(), MaterialSku = x.MaterialSku.Trim(), OrderedQuantity = x.OrderedQuantity,
                ReceivedQuantity = 0, Uom = x.Uom.Trim(), UnitPrice = x.UnitPrice
            }).ToList()
        };
        db.PurchaseOrders.Add(entity);
        db.SaveChanges();
        return (ToDto(entity, supplier), null);
    }

    public IReadOnlyList<PurchaseOrderDto> GetPurchaseOrders(string tenantKey, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.PurchaseOrders.AsNoTracking().Include(x => x.Lines).Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.OrderByDescending(x => x.OrderedAt).Take(Math.Clamp(limit, 1, 200)).AsEnumerable()
            .Select(x => ToDto(x, db.Suppliers.AsNoTracking().Single(s => s.Id == x.SupplierId))).ToList();
    }

    public (PurchaseOrderDto? Order, string? Error) UpdatePurchaseOrder(string tenantKey, Guid purchaseOrderId, UpdatePurchaseOrderRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var order = db.PurchaseOrders.Include(x => x.Lines).SingleOrDefault(x => x.Id == purchaseOrderId);
        if (order is null) return (null, "purchase_order_not_found");
        if (!order.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
        if (!order.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase)) return (null, "purchase_order_not_editable");
        var supplier = db.Suppliers.SingleOrDefault(x => x.Id == request.SupplierId && x.TenantKey == tenantKey && x.Active);
        if (supplier is null) return (null, "supplier_not_found");
        if (string.IsNullOrWhiteSpace(request.OrderNumber) || string.IsNullOrWhiteSpace(request.Currency) || request.Lines is null || request.Lines.Count == 0 || request.Lines.Any(x => string.IsNullOrWhiteSpace(x.MaterialSku) || string.IsNullOrWhiteSpace(x.Uom) || x.OrderedQuantity <= 0 || x.UnitPrice < 0)) return (null, "invalid_purchase_order");
        if (db.PurchaseOrders.Any(x => x.Id != purchaseOrderId && x.TenantKey == tenantKey && x.OrderNumber == request.OrderNumber.Trim())) return (null, "purchase_order_exists");
        order.SupplierId = supplier.Id; order.OrderNumber = request.OrderNumber.Trim(); order.Currency = request.Currency.Trim().ToUpperInvariant(); order.ExpectedAt = request.ExpectedAt;
        db.PurchaseOrderLines.RemoveRange(order.Lines);
        db.SaveChanges();
        order.Lines = request.Lines.Select(x => new ManufacturingPurchaseOrderLineEntity { Id = Guid.NewGuid(), PurchaseOrderId = order.Id, MaterialSku = x.MaterialSku.Trim(), OrderedQuantity = x.OrderedQuantity, ReceivedQuantity = 0, Uom = x.Uom.Trim(), UnitPrice = x.UnitPrice }).ToList();
        db.PurchaseOrderLines.AddRange(order.Lines);
        db.SaveChanges();
        return (ToDto(order, supplier), null);
    }

    public (PurchaseOrderDto? Order, string? Error) UpdatePurchaseOrderStatus(string tenantKey, Guid purchaseOrderId, string status)
    {
        using var db = dbFactory.CreateDbContext();
        var order = db.PurchaseOrders.Include(x => x.Lines).SingleOrDefault(x => x.Id == purchaseOrderId);
        if (order is null) return (null, "purchase_order_not_found");
        if (!order.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
        var normalized = status.Trim();
        var allowed = normalized switch
        {
            "Approved" when order.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase) => true,
            "Cancelled" when order.Status is "Draft" or "Approved" => true,
            _ => false,
        };
        if (!allowed) return (null, "invalid_purchase_order_transition");
        order.Status = normalized;
        db.SaveChanges();
        var supplier = db.Suppliers.AsNoTracking().Single(s => s.Id == order.SupplierId);
        return (ToDto(order, supplier), null);
    }

    public (InboundReceiptDto? Receipt, string? Error) ReceiveInboundLot(string tenantKey, ReceiveInboundLotRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var order = db.PurchaseOrders.Include(x => x.Lines).SingleOrDefault(x => x.Id == request.PurchaseOrderId);
        if (order is null) return (null, "purchase_order_not_found");
        var line = order.Lines.SingleOrDefault(x => x.Id == request.PurchaseOrderLineId);
        if (line is null) return (null, "purchase_order_line_not_found");
        var policyError = ProcurementPolicy.ValidateInboundReceipt(new InboundReceiptValidationInput(
            request.Quantity, tenantKey, order.TenantKey, order.Status, line.MaterialSku, request.MaterialSku,
            line.ReceivedQuantity, line.OrderedQuantity));
        if (policyError is not null) return (null, policyError);
        if (db.InboundReceipts.Any(x => x.TenantKey == tenantKey && x.ReceiptNumber == request.ReceiptNumber.Trim())) return (null, "receipt_number_exists");
        if (db.InboundReceipts.Any(x => x.TenantKey == tenantKey && x.SupplierId == order.SupplierId && x.SupplierLotCode == request.SupplierLotCode.Trim())) return (null, "supplier_lot_exists");

        var now = DateTimeOffset.UtcNow;
        var lot = new ManufacturingLotEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, Sku = line.MaterialSku, Quantity = request.Quantity,
            Uom = line.Uom, Disposition = "Quarantined", BestBefore = request.ExpiryDate, CreatedAt = now
        };
        var receipt = new ManufacturingInboundReceiptEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, ReceiptNumber = request.ReceiptNumber.Trim(),
            PurchaseOrderId = order.Id, PurchaseOrderLineId = line.Id, LotId = lot.Id, SupplierId = order.SupplierId,
            SupplierLotCode = request.SupplierLotCode.Trim(), FacilityId = request.FacilityId.Trim(),
            Quantity = request.Quantity, Uom = line.Uom, ReceivedAt = request.ReceivedAt ?? now
        };
        line.ReceivedQuantity += request.Quantity;
        if (order.Lines.All(x => x.ReceivedQuantity == x.OrderedQuantity)) order.Status = "Closed";
        else if (order.Lines.Any(x => x.ReceivedQuantity > 0)) order.Status = "PartiallyReceived";
        db.Lots.Add(lot);
        db.InboundReceipts.Add(receipt);
        db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, LotId = lot.Id, TransactionType = "Receipt",
            Quantity = receipt.Quantity, Uom = receipt.Uom, FacilityId = receipt.FacilityId,
            StockStatus = lot.Disposition, CorrelationId = receipt.Id, OccurredAt = receipt.ReceivedAt
        });
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.RawMaterialLotReceived.v1",
            Content = JsonSerializer.Serialize(new
            {
                eventId = receipt.Id, schemaVersion = 1, occurredAt = receipt.ReceivedAt,
                correlationId = receipt.Id, facilityId = receipt.FacilityId, lotId = lot.Id,
                materialId = lot.Sku, supplierId = receipt.SupplierId, supplierLotCode = receipt.SupplierLotCode,
                receiptId = receipt.Id, quantity = receipt.Quantity, uom = receipt.Uom,
                receivedAt = receipt.ReceivedAt, expiryDate = lot.BestBefore, tenantKey
            }),
            OccurredOn = receipt.ReceivedAt.UtcDateTime, Status = "Pending"
        });
        db.SaveChanges();
        return (new InboundReceiptDto(receipt.Id, receipt.TenantKey, receipt.ReceiptNumber, receipt.PurchaseOrderId, receipt.PurchaseOrderLineId, receipt.LotId, receipt.SupplierId, receipt.SupplierLotCode, receipt.FacilityId, receipt.Quantity, receipt.Uom, receipt.ReceivedAt, lot.Disposition, lot.BestBefore), null);
    }

    public IReadOnlyList<InboundReceiptDto> GetInboundReceipts(string tenantKey, Guid? purchaseOrderId, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.InboundReceipts.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (purchaseOrderId.HasValue) query = query.Where(x => x.PurchaseOrderId == purchaseOrderId.Value);
        var receipts = query.OrderByDescending(x => x.ReceivedAt).Take(Math.Clamp(limit, 1, 500)).ToList();
        var lotIds = receipts.Select(x => x.LotId).ToArray();
        var lots = db.Lots.AsNoTracking().Where(x => lotIds.Contains(x.Id)).ToDictionary(x => x.Id);
        return receipts.Select(x =>
        {
            lots.TryGetValue(x.LotId, out var lot);
            return new InboundReceiptDto(x.Id, x.TenantKey, x.ReceiptNumber, x.PurchaseOrderId, x.PurchaseOrderLineId, x.LotId, x.SupplierId, x.SupplierLotCode, x.FacilityId, x.Quantity, x.Uom, x.ReceivedAt, lot?.Disposition ?? "Quarantined", lot?.BestBefore);
        }).ToList();
    }

    public (IReadOnlyList<InboundReceiptDto> Receipts, string? Error) ReceiveInboundBatch(string tenantKey, Guid purchaseOrderId, ReceiveInboundBatchRequest request)
    {
        if (request.Receipts is null || request.Receipts.Count == 0) return (Array.Empty<InboundReceiptDto>(), "invalid_inbound_batch");
        var receipts = new List<InboundReceiptDto>();
        foreach (var item in request.Receipts)
        {
            if (item.PurchaseOrderId != purchaseOrderId) return (Array.Empty<InboundReceiptDto>(), "invalid_inbound_batch");
            var result = ReceiveInboundLot(tenantKey, item);
            if (result.Error is not null) return (Array.Empty<InboundReceiptDto>(), result.Error);
            receipts.Add(result.Receipt!);
        }
        return (receipts, null);
    }

    private static SupplierDto ToDto(ManufacturingSupplierEntity x) => new(x.Id, x.TenantKey, x.Code, x.Name, x.Active, x.CreatedAt);
    private static PurchaseOrderDto ToDto(ManufacturingPurchaseOrderEntity x, ManufacturingSupplierEntity supplier) =>
        new(x.Id, x.TenantKey, x.OrderNumber, supplier.Id, supplier.Code, x.Status, x.Currency, x.OrderedAt, x.ExpectedAt,
            x.Lines.Select(l => new PurchaseOrderLineDto(l.Id, l.MaterialSku, l.OrderedQuantity, l.ReceivedQuantity, l.Uom, l.UnitPrice)).ToList(), supplier.Name);
}

public sealed class ManufacturingSupplierEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingFacilityEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingWarehouseEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid FacilityId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingStorageLocationEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingUomEntity { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string Dimension { get; set; } = ""; public bool Active { get; set; } public DateTimeOffset CreatedAt { get; set; } }
public sealed class ManufacturingUomConversionEntity { public Guid Id { get; set; } public string FromCode { get; set; } = ""; public string ToCode { get; set; } = ""; public decimal Factor { get; set; } public bool Active { get; set; } public DateTimeOffset CreatedAt { get; set; } }
public sealed class ManufacturingMaterialEntity { public Guid Id { get; set; } public string TenantKey { get; set; } = ""; public string Sku { get; set; } = ""; public string Name { get; set; } = ""; public string BaseUomCode { get; set; } = ""; public string MaterialType { get; set; } = "RawMaterial"; public bool Active { get; set; } public DateTimeOffset CreatedAt { get; set; } }
public sealed class ManufacturingProductEntity { public Guid Id { get; set; } public string TenantKey { get; set; } = ""; public string Sku { get; set; } = ""; public string Name { get; set; } = ""; public string BaseUomCode { get; set; } = ""; public bool Active { get; set; } public DateTimeOffset CreatedAt { get; set; } }
public sealed class ManufacturingSupplierRfqEntity { public Guid Id { get; set; } public string TenantKey { get; set; } = ""; public string RfqNumber { get; set; } = ""; public string MaterialSku { get; set; } = ""; public decimal Quantity { get; set; } public string Uom { get; set; } = ""; public string Status { get; set; } = "Open"; public DateTimeOffset? NeededBy { get; set; } public DateTimeOffset CreatedAt { get; set; } }
public sealed class ManufacturingSupplierQuotationEntity { public Guid Id { get; set; } public string TenantKey { get; set; } = ""; public Guid SupplierRfqId { get; set; } public Guid SupplierId { get; set; } public decimal UnitPrice { get; set; } public string Currency { get; set; } = "VND"; public int LeadTimeDays { get; set; } public string Status { get; set; } = "Submitted"; public string? Notes { get; set; } public DateTimeOffset CreatedAt { get; set; } }

public sealed class ManufacturingPurchaseOrderEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid SupplierId { get; set; }
    public string OrderNumber { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public string Currency { get; set; } = "VND";
    public DateTimeOffset OrderedAt { get; set; }
    public DateTimeOffset? ExpectedAt { get; set; }
    public List<ManufacturingPurchaseOrderLineEntity> Lines { get; set; } = [];
}

public sealed class ManufacturingPurchaseOrderLineEntity
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string MaterialSku { get; set; } = "";
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public string Uom { get; set; } = "";
    public decimal UnitPrice { get; set; }
}

public sealed class ManufacturingInboundReceiptEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string ReceiptNumber { get; set; } = "";
    public Guid PurchaseOrderId { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public Guid LotId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierLotCode { get; set; } = "";
    public string FacilityId { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
}
