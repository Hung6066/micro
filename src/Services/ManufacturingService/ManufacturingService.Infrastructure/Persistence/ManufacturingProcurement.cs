using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;
using His.Hope.ManufacturingService.Domain;
using His.Hope.SharedKernel.Domain.Common;

public sealed class ManufacturingProcurementStore(IDbContextFactory<ManufacturingDbContext> dbFactory) : IManufacturingProcurementStore
{
    public SupplierDto CreateSupplier(CreateSupplierRequest request)
    {
        var profileError = SupplierGovernancePolicy.ValidateProfile(ManufacturingStatusCodes.Draft, request.RiskLevel, request.CountryCode?.Trim().ToUpperInvariant(), request.ContactEmail?.Trim());
        if (profileError is not null) throw new InvalidOperationException(profileError);
        using var db = dbFactory.CreateDbContext();
        if (db.Suppliers.Any(x => x.TenantKey == request.TenantKey && x.Code == request.Code))
            Guard.Against.Conflict(true, ManufacturingErrorCodes.SupplierCodeExists);
        var entity = new ManufacturingSupplierEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), Code = request.Code.Trim(),
            Name = request.Name.Trim(), Active = request.Active, CreatedAt = DateTimeOffset.UtcNow,
            LegalName = request.LegalName?.Trim() ?? request.Name.Trim(), TaxIdentificationNumber = request.TaxIdentificationNumber?.Trim(),
            ContactName = request.ContactName?.Trim(), ContactEmail = request.ContactEmail?.Trim(), ContactPhone = request.ContactPhone?.Trim(),
            CountryCode = request.CountryCode?.Trim().ToUpperInvariant(), Address = request.Address?.Trim(), RiskLevel = request.RiskLevel.Trim(),
            ApprovalStatus = ManufacturingStatusCodes.Draft, CreatedBy = request.CreatedBy?.Trim() ?? "system", UpdatedAt = DateTimeOffset.UtcNow
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
        var profileError = SupplierGovernancePolicy.ValidateProfile(ManufacturingStatusCodes.Draft, request.RiskLevel, request.CountryCode?.Trim().ToUpperInvariant(), request.ContactEmail?.Trim());
        if (profileError is not null) return (null, profileError);
        using var db = dbFactory.CreateDbContext();
        var supplier = db.Suppliers.SingleOrDefault(x => x.Id == supplierId);
        if (supplier is null) return (null, ManufacturingErrorCodes.SupplierNotFound);
        if (!supplier.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);
        if (db.Suppliers.Any(x => x.Id != supplierId && x.TenantKey == tenantKey && x.Code == request.Code.Trim())) return (null, ManufacturingErrorCodes.SupplierCodeExists);
        supplier.Code = request.Code.Trim();
        supplier.Name = request.Name.Trim();
        supplier.Active = request.Active;
        supplier.LegalName = request.LegalName?.Trim() ?? request.Name.Trim();
        supplier.TaxIdentificationNumber = request.TaxIdentificationNumber?.Trim();
        supplier.ContactName = request.ContactName?.Trim();
        supplier.ContactEmail = request.ContactEmail?.Trim();
        supplier.ContactPhone = request.ContactPhone?.Trim();
        supplier.CountryCode = request.CountryCode?.Trim().ToUpperInvariant();
        supplier.Address = request.Address?.Trim();
        supplier.RiskLevel = request.RiskLevel.Trim();
        supplier.UpdatedAt = DateTimeOffset.UtcNow;
        db.SaveChanges();
        return (ToDto(supplier), null);
    }

    public (SupplierDto? Supplier, string? Error) UpdateSupplierApproval(string tenantKey, Guid supplierId, SupplierApprovalRequest request, string actor)
    {
        using var db = dbFactory.CreateDbContext();
        var supplier = db.Suppliers.SingleOrDefault(x => x.Id == supplierId);
        if (supplier is null) return (null, ManufacturingErrorCodes.SupplierNotFound);
        if (!supplier.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);

        var nextStatus = request.Status.Trim();
        var transitionError = SupplierGovernancePolicy.ValidateApprovalTransition(supplier.ApprovalStatus, nextStatus);
        if (transitionError is not null) return (null, transitionError);

        supplier.ApprovalStatus = nextStatus;
        supplier.LastReviewedAt = DateTimeOffset.UtcNow;
        supplier.UpdatedAt = supplier.LastReviewedAt;
        if (nextStatus.Equals(ManufacturingStatusCodes.Approved, StringComparison.OrdinalIgnoreCase))
        {
            supplier.ApprovedBy = actor;
            supplier.ApprovedAt = supplier.LastReviewedAt;
            var activeMaterials = db.Materials.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Active).ToList();
            var approvedSkus = db.SupplierMaterialApprovals.Where(x => x.TenantKey == tenantKey && x.SupplierId == supplier.Id).Select(x => x.MaterialSku).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var material in activeMaterials.Where(x => !approvedSkus.Contains(x.Sku)))
            {
                db.SupplierMaterialApprovals.Add(new ManufacturingSupplierMaterialApprovalEntity
                {
                    Id = Guid.NewGuid(), TenantKey = tenantKey, SupplierId = supplier.Id, MaterialSku = material.Sku,
                    ApprovedUom = material.BaseUomCode, EffectiveFrom = supplier.ApprovedAt.Value, Status = ManufacturingStatusCodes.Approved,
                    Notes = "Auto-created from supplier approval", CreatedAt = supplier.ApprovedAt.Value, CreatedBy = actor
                });
            }
        }
        else if (!nextStatus.Equals(ManufacturingStatusCodes.Suspended, StringComparison.OrdinalIgnoreCase))
        {
            supplier.ApprovedBy = null;
            supplier.ApprovedAt = null;
        }

        db.AuditEvents.Add(new ManufacturingAuditEventEntity
        {
            Id = Guid.NewGuid(), TenantKey = supplier.TenantKey, EntityType = "Supplier", EntityId = supplier.Id,
            Action = "approval_status_changed", Actor = actor, OccurredAt = supplier.LastReviewedAt.Value,
            Details = JsonSerializer.Serialize(new { status = supplier.ApprovalStatus, notes = request.Notes })
        });
        db.SaveChanges();
        return (ToDto(supplier), null);
    }

    public (SupplierCertificateDto? Certificate, string? Error) CreateSupplierCertificate(string tenantKey, Guid supplierId, CreateSupplierCertificateRequest request, string actor)
    {
        if (string.IsNullOrWhiteSpace(request.CertificateType) || string.IsNullOrWhiteSpace(request.CertificateNumber) || string.IsNullOrWhiteSpace(request.Issuer) || request.ExpiresAt <= request.IssuedAt)
            return (null, ManufacturingErrorCodes.InvalidSupplierCertificate);
        using var db = dbFactory.CreateDbContext();
        var supplier = db.Suppliers.SingleOrDefault(x => x.Id == supplierId && x.TenantKey == tenantKey);
        if (supplier is null) return (null, ManufacturingErrorCodes.SupplierNotFound);
        if (db.SupplierCertificates.Any(x => x.TenantKey == tenantKey && x.SupplierId == supplierId && x.CertificateNumber == request.CertificateNumber.Trim()))
            return (null, "supplier_certificate_exists");
        var entity = new ManufacturingSupplierCertificateEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, SupplierId = supplierId,
            CertificateType = request.CertificateType.Trim(), CertificateNumber = request.CertificateNumber.Trim(), Issuer = request.Issuer.Trim(),
            IssuedAt = request.IssuedAt, ExpiresAt = request.ExpiresAt, Status = request.ExpiresAt <= DateTimeOffset.UtcNow ? "Expired" : "Active",
            EvidenceReference = request.EvidenceReference?.Trim(), CreatedAt = DateTimeOffset.UtcNow, CreatedBy = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim()
        };
        db.SupplierCertificates.Add(entity);
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<SupplierCertificateDto> GetSupplierCertificates(string tenantKey, Guid supplierId, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        return db.SupplierCertificates.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.SupplierId == supplierId)
            .OrderByDescending(x => x.ExpiresAt).Take(Math.Clamp(limit, 1, 100)).AsEnumerable().Select(ToDto).ToList();
    }

    public (SupplierMaterialApprovalDto? Approval, string? Error) CreateSupplierMaterialApproval(string tenantKey, Guid supplierId, CreateSupplierMaterialApprovalRequest request, string actor)
    {
        if (string.IsNullOrWhiteSpace(request.MaterialSku) || string.IsNullOrWhiteSpace(request.ApprovedUom) || (request.EffectiveTo.HasValue && request.EffectiveTo.Value <= request.EffectiveFrom))
            return (null, ManufacturingErrorCodes.InvalidSupplierMaterialApproval);
        using var db = dbFactory.CreateDbContext();
        var supplier = db.Suppliers.AsNoTracking().SingleOrDefault(x => x.Id == supplierId && x.TenantKey == tenantKey);
        if (supplier is null) return (null, ManufacturingErrorCodes.SupplierNotFound);
        var materialSku = request.MaterialSku.Trim();
        if (!db.Materials.Any(x => x.TenantKey == tenantKey && x.Sku == materialSku && x.Active)) return (null, ManufacturingErrorCodes.MaterialNotFound);
        if (db.SupplierMaterialApprovals.Any(x => x.TenantKey == tenantKey && x.SupplierId == supplierId && x.MaterialSku == materialSku))
            return (null, "supplier_material_approval_exists");
        var entity = new ManufacturingSupplierMaterialApprovalEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, SupplierId = supplierId, MaterialSku = materialSku,
            ApprovedUom = request.ApprovedUom.Trim(), EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo, Status = ManufacturingStatusCodes.Approved, Notes = request.Notes?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow, CreatedBy = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim()
        };
        db.SupplierMaterialApprovals.Add(entity); db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<SupplierMaterialApprovalDto> GetSupplierMaterialApprovals(string tenantKey, Guid supplierId, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        return db.SupplierMaterialApprovals.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.SupplierId == supplierId)
            .OrderBy(x => x.MaterialSku).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    public (PurchaseOrderDto? Order, string? Error) CreatePurchaseOrder(CreatePurchaseOrderRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var supplier = db.Suppliers.SingleOrDefault(x => x.Id == request.SupplierId);
        if (supplier is null) return (null, ManufacturingErrorCodes.SupplierNotFound);
        var lineCount = request.Lines?.Count ?? 0;
        var policyError = ProcurementPolicy.ValidatePurchaseOrder(new PurchaseOrderValidationInput(
            request.Status, request.TenantKey, supplier.TenantKey, supplier.Active, request.OrderNumber, lineCount));
        if (policyError is not null) return (null, policyError);
        if (!SupplierGovernancePolicy.IsPurchasable(supplier.ApprovalStatus)) return (null, ManufacturingErrorCodes.SupplierNotApproved);
        if (string.IsNullOrWhiteSpace(request.OrderNumber) || string.IsNullOrWhiteSpace(request.Currency) || request.Lines is null || request.Lines.Count == 0 || request.Lines.Any(x => string.IsNullOrWhiteSpace(x.MaterialSku) || string.IsNullOrWhiteSpace(x.Uom) || x.OrderedQuantity <= 0 || x.UnitPrice < 0)) return (null, "invalid_purchase_order");
        var lineError = ValidatePurchaseOrderLines(db, request.TenantKey, supplier.Id, request.Lines);
        if (lineError is not null) return (null, lineError);
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
        EntityStatusHistoryStore.Append(db, "purchase-order", entity.Id, entity.TenantKey, "", entity.Status, "system", entity.OrderedAt);
        db.SaveChanges();
        return (ToDto(entity, supplier), null);
    }

    public IReadOnlyList<PurchaseOrderDto> GetPurchaseOrders(string tenantKey, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.PurchaseOrders.AsNoTracking().Include(x => x.Lines).Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var orders = query.OrderByDescending(x => x.OrderedAt).Take(Math.Clamp(limit, 1, 200)).ToList();
        var supplierIds = orders.Select(x => x.SupplierId).Distinct().ToList();
        var suppliers = db.Suppliers.AsNoTracking()
            .Where(x => supplierIds.Contains(x.Id))
            .ToDictionary(x => x.Id);
        return orders.Select(x => ToDto(x, suppliers[x.SupplierId])).ToList();
    }

    public IReadOnlyList<EntityStatusHistoryDto> GetPurchaseOrderStatusHistory(string tenantKey, Guid purchaseOrderId)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.PurchaseOrders.AsNoTracking().SingleOrDefault(x => x.Id == purchaseOrderId);
        if (entity is null || !entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase))
            return [];

        var persisted = EntityStatusHistoryStore.Get(db, tenantKey, "purchase-order", purchaseOrderId);
        if (persisted.Count > 0)
            return persisted;

        return ManufacturingStatusHistoryBuilder.ForPurchaseOrder(
            entity.Id,
            entity.TenantKey,
            entity.Status,
            entity.OrderedAt);
    }

    public (PurchaseOrderDto? Order, string? Error) UpdatePurchaseOrder(string tenantKey, Guid purchaseOrderId, UpdatePurchaseOrderRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var order = db.PurchaseOrders.Include(x => x.Lines).SingleOrDefault(x => x.Id == purchaseOrderId);
        if (order is null) return (null, "purchase_order_not_found");
        if (!order.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);
        if (!order.Status.Equals(ManufacturingStatusCodes.Draft, StringComparison.OrdinalIgnoreCase)) return (null, "purchase_order_not_editable");
        var supplier = db.Suppliers.SingleOrDefault(x => x.Id == request.SupplierId && x.TenantKey == tenantKey && x.Active);
        if (supplier is null) return (null, ManufacturingErrorCodes.SupplierNotFound);
        if (string.IsNullOrWhiteSpace(request.OrderNumber) || string.IsNullOrWhiteSpace(request.Currency) || request.Lines is null || request.Lines.Count == 0 || request.Lines.Any(x => string.IsNullOrWhiteSpace(x.MaterialSku) || string.IsNullOrWhiteSpace(x.Uom) || x.OrderedQuantity <= 0 || x.UnitPrice < 0)) return (null, "invalid_purchase_order");
        var lineError = ValidatePurchaseOrderLines(db, tenantKey, supplier.Id, request.Lines);
        if (lineError is not null) return (null, lineError);
        if (db.PurchaseOrders.Any(x => x.Id != purchaseOrderId && x.TenantKey == tenantKey && x.OrderNumber == request.OrderNumber.Trim())) return (null, "purchase_order_exists");
        order.SupplierId = supplier.Id; order.OrderNumber = request.OrderNumber.Trim(); order.Currency = request.Currency.Trim().ToUpperInvariant(); order.ExpectedAt = request.ExpectedAt;
        db.PurchaseOrderLines.RemoveRange(order.Lines);
        db.SaveChanges();
        order.Lines = request.Lines.Select(x => new ManufacturingPurchaseOrderLineEntity { Id = Guid.NewGuid(), PurchaseOrderId = order.Id, MaterialSku = x.MaterialSku.Trim(), OrderedQuantity = x.OrderedQuantity, ReceivedQuantity = 0, Uom = x.Uom.Trim(), UnitPrice = x.UnitPrice }).ToList();
        db.PurchaseOrderLines.AddRange(order.Lines);
        db.SaveChanges();
        return (ToDto(order, supplier), null);
    }

    private static string? ValidatePurchaseOrderLines(ManufacturingDbContext db, string tenantKey, Guid supplierId, IReadOnlyList<PurchaseOrderLineRequest>? lines)
    {
        if (lines is null || lines.Count == 0) return "invalid_purchase_order";
        var normalizedSkus = new List<string>(lines.Count);
        var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var sku = line.MaterialSku.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(sku) || !seenSkus.Add(sku)) return "invalid_purchase_order";
            normalizedSkus.Add(sku);
        }
        var materialSkus = db.Materials.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.Active && normalizedSkus.Contains(x.Sku.ToUpper()))
            .Select(x => x.Sku)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (normalizedSkus.Any(x => !materialSkus.Contains(x))) return ManufacturingErrorCodes.MaterialNotFound;
        var now = DateTimeOffset.UtcNow;
        var approvedSkus = db.SupplierMaterialApprovals.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.SupplierId == supplierId && x.Status == ManufacturingStatusCodes.Approved && x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now) && normalizedSkus.Contains(x.MaterialSku))
            .Select(x => x.MaterialSku)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return normalizedSkus.Any(x => !approvedSkus.Contains(x)) ? ManufacturingErrorCodes.SupplierMaterialNotApproved : null;
    }

    private static SupplierCertificateDto ToDto(ManufacturingSupplierCertificateEntity x) =>
        new(x.Id, x.TenantKey, x.SupplierId, x.CertificateType, x.CertificateNumber, x.Issuer, x.IssuedAt, x.ExpiresAt, x.Status, x.EvidenceReference, x.CreatedAt, x.CreatedBy);
    private static SupplierMaterialApprovalDto ToDto(ManufacturingSupplierMaterialApprovalEntity x) =>
        new(x.Id, x.TenantKey, x.SupplierId, x.MaterialSku, x.ApprovedUom, x.EffectiveFrom, x.EffectiveTo, x.Status, x.Notes, x.CreatedAt, x.CreatedBy);

    public (PurchaseOrderDto? Order, string? Error) UpdatePurchaseOrderStatus(string tenantKey, Guid purchaseOrderId, string status)
    {
        using var db = dbFactory.CreateDbContext();
        var order = db.PurchaseOrders.Include(x => x.Lines).SingleOrDefault(x => x.Id == purchaseOrderId);
        if (order is null) return (null, "purchase_order_not_found");
        if (!order.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);
        var normalized = status.Trim();
        var allowed = normalized switch
        {
            ManufacturingStatusCodes.Approved when order.Status.Equals(ManufacturingStatusCodes.Draft, StringComparison.OrdinalIgnoreCase) => true,
            ManufacturingStatusCodes.Cancelled when order.Status is ManufacturingStatusCodes.Draft or ManufacturingStatusCodes.Approved => true,
            _ => false,
        };
        if (!allowed) return (null, "invalid_purchase_order_transition");
        var previousStatus = order.Status;
        order.Status = normalized;
        EntityStatusHistoryStore.Append(db, "purchase-order", order.Id, tenantKey, previousStatus, normalized, "operator", DateTimeOffset.UtcNow);
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
        var receivedAt = request.ReceivedAt ?? now;
        var lotCode = string.IsNullOrWhiteSpace(request.TraceabilityLotCode)
            ? $"LOT-{receivedAt:yyyyMMdd}-{Guid.NewGuid():N}"
            : request.TraceabilityLotCode.Trim().ToUpperInvariant();
        var traceabilityError = LotTraceabilityPolicy.Validate(new LotTraceabilityProfile(
            lotCode, "RawMaterial", request.OriginCountryCode?.Trim().ToUpperInvariant(), request.ManufacturedOn,
            request.ExpiryDate, request.FacilityId.Trim(), request.StorageLocationCode?.Trim()));
        if (traceabilityError is not null) return (null, traceabilityError);
        var acceptedQuantity = request.AcceptedQuantity ?? request.Quantity;
        var rejectedQuantity = request.RejectedQuantity ?? 0;
        if (acceptedQuantity < 0 || rejectedQuantity < 0 || acceptedQuantity + rejectedQuantity != request.Quantity)
            return (null, "invalid_receipt_quantity_balance");
        var lot = new ManufacturingLotEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, Sku = line.MaterialSku, Quantity = request.Quantity,
            Uom = line.Uom, Disposition = "Quarantined", BestBefore = request.ExpiryDate, LotCode = lotCode,
            LotType = "RawMaterial", OriginCountryCode = request.OriginCountryCode?.Trim().ToUpperInvariant(),
            ManufacturedOn = request.ManufacturedOn, ReceivedAt = receivedAt, FacilityCode = request.FacilityId.Trim(),
            StorageLocationCode = request.StorageLocationCode?.Trim(), CertificateOfAnalysisReference = request.CertificateOfAnalysisReference?.Trim(),
            SourceLotCode = request.SupplierLotCode.Trim(), QualityStatus = ManufacturingStatusCodes.Pending, CreatedBy = request.ReceivedBy?.Trim() ?? "system", CreatedAt = now
        };
        var receipt = new ManufacturingInboundReceiptEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, ReceiptNumber = request.ReceiptNumber.Trim(),
            PurchaseOrderId = order.Id, PurchaseOrderLineId = line.Id, LotId = lot.Id, SupplierId = order.SupplierId,
            SupplierLotCode = request.SupplierLotCode.Trim(), FacilityId = request.FacilityId.Trim(),
            Quantity = request.Quantity, Uom = line.Uom, ReceivedAt = receivedAt,
            StorageLocationCode = request.StorageLocationCode?.Trim(), DeliveryNoteNumber = request.DeliveryNoteNumber?.Trim(),
            CarrierName = request.CarrierName?.Trim(), VehicleReference = request.VehicleReference?.Trim(),
            TemperatureOnReceiptC = request.TemperatureOnReceiptC, CertificateOfAnalysisReference = request.CertificateOfAnalysisReference?.Trim(),
            ReceivedBy = request.ReceivedBy?.Trim(), AcceptedQuantity = acceptedQuantity, RejectedQuantity = rejectedQuantity
        };
        line.ReceivedQuantity += request.Quantity;
        var previousPoStatus = order.Status;
        if (order.Lines.All(x => x.ReceivedQuantity == x.OrderedQuantity)) order.Status = ManufacturingStatusCodes.Closed;
        else if (order.Lines.Any(x => x.ReceivedQuantity > 0)) order.Status = "PartiallyReceived";
        if (!order.Status.Equals(previousPoStatus, StringComparison.OrdinalIgnoreCase))
        {
            EntityStatusHistoryStore.Append(
                db,
                "purchase-order",
                order.Id,
                tenantKey,
                previousPoStatus,
                order.Status,
                request.ReceivedBy?.Trim() ?? "system",
                receivedAt);
        }
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
                receivedAt = receipt.ReceivedAt, expiryDate = lot.BestBefore, traceabilityLotCode = lot.LotCode,
                originCountryCode = lot.OriginCountryCode, storageLocationCode = lot.StorageLocationCode,
                certificateOfAnalysisReference = lot.CertificateOfAnalysisReference, tenantKey
            }),
            OccurredOn = receipt.ReceivedAt.UtcDateTime, Status = ManufacturingStatusCodes.Pending
        });
        db.SaveChanges();
        return (ToDto(receipt, lot), null);
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
            return ToDto(x, lot);
        }).ToList();
    }

    public (IReadOnlyList<InboundReceiptDto> Receipts, string? Error) ReceiveInboundBatch(string tenantKey, Guid purchaseOrderId, ReceiveInboundBatchRequest request)
    {
        if (request.Receipts is null || request.Receipts.Count == 0) return (Array.Empty<InboundReceiptDto>(), ManufacturingErrorCodes.InvalidInboundBatch);
        var receipts = new List<InboundReceiptDto>();
        foreach (var item in request.Receipts)
        {
            if (item.PurchaseOrderId != purchaseOrderId) return (Array.Empty<InboundReceiptDto>(), ManufacturingErrorCodes.InvalidInboundBatch);
            var result = ReceiveInboundLot(tenantKey, item);
            if (result.Error is not null) return (Array.Empty<InboundReceiptDto>(), result.Error);
            receipts.Add(result.Receipt!);
        }
        return (receipts, null);
    }

    private static SupplierDto ToDto(ManufacturingSupplierEntity x) => new(
        x.Id, x.TenantKey, x.Code, x.Name, x.Active, x.CreatedAt, x.LegalName, x.TaxIdentificationNumber,
        x.ContactName, x.ContactEmail, x.ContactPhone, x.CountryCode, x.Address, x.RiskLevel, x.ApprovalStatus,
        x.ApprovedBy, x.ApprovedAt, x.LastReviewedAt, x.CreatedBy, x.UpdatedAt);
    private static InboundReceiptDto ToDto(ManufacturingInboundReceiptEntity x, ManufacturingLotEntity? lot) =>
        new(x.Id, x.TenantKey, x.ReceiptNumber, x.PurchaseOrderId, x.PurchaseOrderLineId, x.LotId, x.SupplierId,
            x.SupplierLotCode, x.FacilityId, x.Quantity, x.Uom, x.ReceivedAt, lot?.Disposition ?? "Quarantined", lot?.BestBefore,
            lot?.LotCode ?? "", x.StorageLocationCode, x.DeliveryNoteNumber, x.CarrierName, x.VehicleReference,
            x.TemperatureOnReceiptC, x.CertificateOfAnalysisReference, x.ReceivedBy, x.AcceptedQuantity, x.RejectedQuantity);
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
    public string LegalName { get; set; } = "";
    public string? TaxIdentificationNumber { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? CountryCode { get; set; }
    public string? Address { get; set; }
    public string RiskLevel { get; set; } = "Standard";
    public string ApprovalStatus { get; set; } = ManufacturingStatusCodes.Draft;
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ManufacturingSupplierCertificateEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid SupplierId { get; set; }
    public string CertificateType { get; set; } = "";
    public string CertificateNumber { get; set; } = "";
    public string Issuer { get; set; } = "";
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string Status { get; set; } = "Active";
    public string? EvidenceReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
}

public sealed class ManufacturingSupplierMaterialApprovalEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid SupplierId { get; set; }
    public string MaterialSku { get; set; } = "";
    public string ApprovedUom { get; set; } = "";
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string Status { get; set; } = ManufacturingStatusCodes.Approved;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
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
public sealed class ManufacturingSupplierQuotationEntity { public Guid Id { get; set; } public string TenantKey { get; set; } = ""; public Guid SupplierRfqId { get; set; } public Guid SupplierId { get; set; } public decimal UnitPrice { get; set; } public string Currency { get; set; } = "VND"; public int LeadTimeDays { get; set; } public string Status { get; set; } = ManufacturingStatusCodes.Submitted; public string? Notes { get; set; } public DateTimeOffset CreatedAt { get; set; } }

public sealed class ManufacturingPurchaseOrderEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid SupplierId { get; set; }
    public string OrderNumber { get; set; } = "";
    public string Status { get; set; } = ManufacturingStatusCodes.Draft;
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
    public string? StorageLocationCode { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public string? CarrierName { get; set; }
    public string? VehicleReference { get; set; }
    public decimal? TemperatureOnReceiptC { get; set; }
    public string? CertificateOfAnalysisReference { get; set; }
    public string? ReceivedBy { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
}
