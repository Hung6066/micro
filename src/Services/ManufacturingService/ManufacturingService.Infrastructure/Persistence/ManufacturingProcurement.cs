using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application;

public sealed class ManufacturingProcurementStore(IDbContextFactory<ManufacturingDbContext> dbFactory)
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
            Uom = line.Uom, Disposition = "Quarantine", BestBefore = request.ExpiryDate, CreatedAt = now
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

    private static SupplierDto ToDto(ManufacturingSupplierEntity x) => new(x.Id, x.TenantKey, x.Code, x.Name, x.Active, x.CreatedAt);
    private static PurchaseOrderDto ToDto(ManufacturingPurchaseOrderEntity x, ManufacturingSupplierEntity supplier) =>
        new(x.Id, x.TenantKey, x.OrderNumber, supplier.Id, supplier.Code, x.Status, x.Currency, x.OrderedAt, x.ExpectedAt,
            x.Lines.Select(l => new PurchaseOrderLineDto(l.Id, l.MaterialSku, l.OrderedQuantity, l.ReceivedQuantity, l.Uom, l.UnitPrice)).ToList());
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
